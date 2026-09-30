document.addEventListener("local-ready", loadPlannerStatus);
document.addEventListener("sources-updated", loadPlannerStatus);

let planningConversation = false;
let clarificationHistory = [];
let pendingQuestion = "";
let streamedText = "";
let planStartedAt = 0;
let planStage = "";
let planTimer = 0;

async function loadPlannerStatus() {
  const status = byId("planner-status");
  const button = byId("planner-button");
  if (!status || !button) return;
  try {
    const response = await authorizedFetch("/api/local/planner");
    const result = await response.json();
    status.textContent = result.isReady
      ? `Sẵn sàng · ${result.models}`
      : result.message;
    button.disabled = !result.isReady || sources.length === 0;
  } catch (error) {
    status.textContent = error.message;
  }
}

byId("planner-button").addEventListener("click", () => runPlanner());
byId("planner-answer-button").addEventListener("click", () => {
  const answer = byId("planner-answer").value.trim();
  if (!answer) {
    byId("planner-answer").focus();
    return;
  }
  clarificationHistory.push({ question: pendingQuestion, answer });
  byId("planner-answer").value = "";
  runPlanner();
});

async function runPlanner() {
  const resultBox = byId("planner-result");
  const button = byId("planner-button");
  const answerButton = byId("planner-answer-button");
  const sourceIds = [...byId("sources").querySelectorAll("input:checked")]
    .map(input => input.value);

  if (!planningConversation) {
    planningConversation = true;
    clarificationHistory = [];
    pendingQuestion = "";
  }
  button.disabled = true;
  answerButton.disabled = true;
  byId("planner-clarification").hidden = true;
  byId("planner-stream").hidden = true;
  byId("planner-stream-text").textContent = "";
  streamedText = "";
  planStartedAt = Date.now();
  planStage = "AGY đang phân tích yêu cầu";
  clearInterval(planTimer);
  planTimer = setInterval(updatePlanTimer, 1000);
  activeJob = null;
  previewReady = false;
  byId("preview").hidden = true;
  byId("preview").removeAttribute("src");
  if (previewUrl) URL.revokeObjectURL(previewUrl);
  previewUrl = "";
  byId("pages").replaceChildren();
  byId("job-summary").textContent = "AGY đang tiếp nhận yêu cầu…";
  byId("pdf-button").disabled = true;
  updatePrintButton();
  resultBox.textContent = "Đang kết nối với AGY…";

  try {
    const requestText = buildRequest();
    const response = await authorizedFetch("/api/local/plan/stream", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sourceIds, userRequest: requestText })
    });
    await readPlanEvents(response);
  } catch (error) {
    resultBox.textContent = error.message;
    byId("job-summary").textContent = "Chưa có kế hoạch mới.";
    planningConversation = false;
    pendingQuestion = "";
    byId("planner-clarification").hidden = true;
  } finally {
    clearInterval(planTimer);
    button.disabled = sources.length === 0 || Boolean(pendingQuestion);
    answerButton.disabled = false;
  }
}

function updatePlanTimer() {
  const seconds = Math.floor((Date.now() - planStartedAt) / 1000);
  byId("planner-result").textContent = `${planStage} · ${seconds} giây`;
}

function buildRequest() {
  return clarificationHistory.reduce((text, exchange) =>
    `${text}\n\nCâu hỏi làm rõ của AGY: ${exchange.question}\nTrả lời của người dùng: ${exchange.answer}`,
  byId("planner-request").value.trim());
}

async function readPlanEvents(response) {
  if (!response.body) throw new Error("Trình duyệt không hỗ trợ nhận luồng phản hồi.");
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  while (true) {
    const { value, done } = await reader.read();
    buffer += decoder.decode(value || new Uint8Array(), { stream: !done });
    const frames = buffer.replaceAll("\r\n", "\n").split("\n\n");
    buffer = frames.pop() || "";
    for (const frame of frames) await handlePlanEvent(frame);
    if (done) break;
  }
  if (buffer.trim()) await handlePlanEvent(buffer);
}

async function handlePlanEvent(frame) {
  const event = frame.match(/^event:\s*(.+)$/m)?.[1];
  const data = frame.match(/^data:\s*(.+)$/m)?.[1];
  if (!event || !data) return;
  const payload = JSON.parse(data);
  if (event === "progress") {
    planStage = payload.message;
    updatePlanTimer();
    if (payload.delta) {
      streamedText += payload.delta;
      byId("planner-stream").hidden = false;
      byId("planner-stream-text").textContent = streamedText;
    }
    return;
  }
  if (event === "error") throw new Error(payload.error);
  if (event !== "complete") return;

  if (!payload.job) {
    pendingQuestion = (payload.questions || []).join(" · ");
    byId("planner-question").textContent = `Cần làm rõ: ${pendingQuestion}`;
    byId("planner-clarification").hidden = false;
    const plannerName = payload.tier === "fast-rules" ? "Luồng nhanh" : "AGY";
    byId("planner-answer-button").textContent = payload.tier === "fast-rules"
      ? "Xác nhận & tiếp tục"
      : "Gửi trả lời cho AGY";
    planStage = `${plannerName} phản hồi sau ${(payload.durationMilliseconds / 1000).toFixed(1)} giây. Trả lời bên dưới để lập tiếp kế hoạch`;
    updatePlanTimer();
    byId("planner-answer").focus();
    return;
  }

  planningConversation = false;
  pendingQuestion = "";
  activeJob = payload.job;
  previewReady = false;
  byId("pdf-button").disabled = false;
  updatePrintButton();
  const plannerName = payload.tier === "fast-rules"
    ? "Luồng nhanh"
    : `AGY ${payload.tier || "planner"} · ${Math.round(payload.confidence * 100)}%`;
  byId("job-summary").textContent =
    `${plannerName} · ${(payload.durationMilliseconds / 1000).toFixed(1)} giây · ` +
    `${activeJob.itemCount} mục · ${activeJob.outputPageCount} trang A4`;
  byId("planner-result").textContent = payload.warnings?.length
    ? `Lưu ý: ${payload.warnings.join(" · ")}`
    : "Đã lập kế hoạch. Kiểm tra preview trước khi xuất PDF hoặc in.";
  renderPageButtons();
  await showPreview(0);
}
