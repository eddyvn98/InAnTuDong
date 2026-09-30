document.addEventListener("local-ready", loadPlannerStatus);
document.addEventListener("sources-updated", loadPlannerStatus);
document.addEventListener("selection-updated", updatePlannerRequestButton);

let streamedText = "";
let planStartedAt = 0;
let planStage = "";
let planTimer = 0;
let plannerReady = false;

async function loadPlannerStatus() {
  const status = byId("planner-status");
  const button = byId("planner-button");
  if (!status || !button) return;
  try {
    const response = await authorizedFetch("/api/local/planner");
    const result = await response.json();
    plannerReady = result.isReady;
    status.textContent = result.isReady
      ? `Sẵn sàng · ${result.models}`
      : result.message;
    updatePlannerRequestButton();
    renderRequestQueue();
  } catch (error) {
    plannerReady = false;
    status.textContent = error.message;
    updatePlannerRequestButton();
    renderRequestQueue();
  }
}

byId("planner-answer-button").addEventListener("click", () => {
  const answer = byId("planner-answer").value.trim();
  if (!answer) {
    byId("planner-answer").focus();
    return;
  }
  if (!activeQueueItem) return;
  activeQueueItem.history.push({ question: activeQueueItem.pendingQuestion, answer });
  activeQueueItem.pendingQuestion = "";
  activeQueueItem.status = "waiting";
  byId("planner-answer").value = "";
  renderRequestQueue();
  runPlannerQueue();
});

async function runPlannerQueue() {
  if (queueRunning || !plannerReady || activeQueueItem?.status === "needs-answer") return;
  queueRunning = true;
  renderRequestQueue();
  try {
    while (true) {
      const next = requestQueue.find(item => item.status === "waiting");
      if (!next) break;
      activeQueueItem = next;
      next.status = "processing";
      renderRequestQueue();
      await runPlanner(next);
      if (next.status === "needs-answer") break;
      activeQueueItem = null;
    }
  } finally {
    queueRunning = false;
    if (activeQueueItem?.status !== "needs-answer") activeQueueItem = null;
    renderRequestQueue();
    byId("remove-sources-button").disabled = selectedSourceIds.size === 0;
  }
}

async function runPlanner(queueItem) {
  const resultBox = byId("planner-result");
  const button = byId("planner-button");
  const answerButton = byId("planner-answer-button");
  const sourceIds = queueItem.sourceIds;
  button.disabled = selectedSourceIds.size === 0;
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
  setPreviewMode("source");
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
    const requestText = buildRequest(queueItem);
    const requestBody = JSON.stringify({ sourceIds, userRequest: requestText });
    const options = {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: requestBody
    };
    try {
      const response = await authorizedFetch("/api/local/plan/stream", options);
      await readPlanEvents(response);
    } catch (error) {
      if (error.status !== 404 && error.status !== 405) throw error;
      planStage = "Đang dùng luồng AGY tương thích";
      updatePlanTimer();
      const response = await authorizedFetch("/api/local/plan", options);
      await handlePlanComplete(await response.json());
    }
  } catch (error) {
    resultBox.textContent = error.message;
    byId("job-summary").textContent = "Chưa có kế hoạch mới.";
    queueItem.status = "error";
    queueItem.pendingQuestion = "";
    byId("planner-clarification").hidden = true;
    renderRequestQueue();
  } finally {
    clearInterval(planTimer);
    updatePlannerRequestButton();
    answerButton.disabled = false;
  }
}

function updatePlanTimer() {
  const seconds = Math.floor((Date.now() - planStartedAt) / 1000);
  byId("planner-result").textContent = `${planStage} · ${seconds} giây`;
}

function buildRequest(queueItem) {
  return queueItem.history.reduce((text, exchange) =>
    `${text}\n\nCâu hỏi làm rõ của AGY: ${exchange.question}\nTrả lời của người dùng: ${exchange.answer}`,
  queueItem.request);
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

  await handlePlanComplete(payload);
}

async function handlePlanComplete(payload) {
  if (!payload.job) {
    activeQueueItem.pendingQuestion = (payload.questions || []).join(" · ");
    activeQueueItem.status = "needs-answer";
    byId("planner-question").textContent = `Cần làm rõ: ${activeQueueItem.pendingQuestion}`;
    byId("planner-clarification").hidden = false;
    const duration = Number(payload.durationMilliseconds || 0);
    planStage = duration
      ? `AGY đã trả lời sau ${(duration / 1000).toFixed(1)} giây. Trả lời bên dưới để AGY lập tiếp kế hoạch`
      : "AGY cần bạn trả lời câu hỏi bên dưới để lập tiếp kế hoạch";
    updatePlanTimer();
    byId("planner-answer").focus();
    renderRequestQueue();
    return;
  }

  activeQueueItem.status = "done";
  activeQueueItem.pendingQuestion = "";
  activeQueueItem.result = payload;
  activeJob = payload.job;
  previewReady = false;
  byId("pdf-button").disabled = false;
  updatePrintButton();
  byId("job-summary").textContent =
    `AGY ${payload.tier || "planner"} · ${Math.round(payload.confidence * 100)}% · ` +
    `${Number(payload.durationMilliseconds) ? `${(payload.durationMilliseconds / 1000).toFixed(1)} giây · ` : ""}` +
    `${activeJob.itemCount} mục · ${activeJob.outputPageCount} trang A4`;
  byId("planner-result").textContent = payload.warnings?.length
    ? `Lưu ý: ${payload.warnings.join(" · ")}`
    : "Đã lập kế hoạch. Kiểm tra preview trước khi xuất PDF hoặc in.";
  renderPageButtons();
  await showPreview(0);
  renderRequestQueue();
}

document.addEventListener("queued-result-selected", async event => {
  const payload = event.detail.result;
  if (!payload?.job) return;
  activeJob = payload.job;
  previewReady = false;
  byId("pdf-button").disabled = false;
  updatePrintButton();
  byId("job-summary").textContent =
    `AGY ${payload.tier || "planner"} · ${Math.round(payload.confidence * 100)}% · ` +
    `${activeJob.itemCount} mục · ${activeJob.outputPageCount} trang A4`;
  renderPageButtons();
  await showPreview(0);
});
