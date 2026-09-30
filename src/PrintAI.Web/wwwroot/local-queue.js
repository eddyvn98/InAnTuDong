let nextQueueId = 1;

function enqueuePlannerRequest() {
  const request = byId("planner-request").value.trim();
  const sourceIds = [...selectedSourceIds];
  if (!sourceIds.length) return showError(new Error("Chọn ít nhất một file cho yêu cầu."));
  if (!canSubmitPlannerRequest())
    return showError(new Error("Xem preview các file đã chọn trước khi gửi yêu cầu."));
  if (!request) return showError(new Error("Nhập yêu cầu in trước khi đưa vào hàng đợi."));

  requestQueue.push({
    id: nextQueueId++,
    order: requestQueue.length + 1,
    request,
    sourceIds,
    history: [],
    pendingQuestion: "",
    status: "waiting",
    result: null
  });
  byId("planner-request").value = "";
  byId("message").textContent = "";
  renderRequestQueue();
}

function renderRequestQueue() {
  const container = byId("request-queue");
  container.replaceChildren();
  if (!requestQueue.length) {
    byId("queue-run-button").disabled = true;
    setIconButton(byId("queue-run-button"), "play", "Hàng đợi trống");
    byId("queue-run-status").textContent = "Hàng đợi trống";
    return;
  }

  const heading = document.createElement("h3");
  heading.textContent = `Hàng đợi · ${requestQueue.length} yêu cầu`;
  container.append(heading);
  for (const item of requestQueue) {
    const row = document.createElement("div");
    row.className = "queue-entry";
    const content = document.createElement("div");
    content.className = "queue-entry-text";
    const title = document.createElement("strong");
    title.textContent = `#${item.order} · ${queueStatusLabel(item.status)}`;
    const prompt = document.createElement("div");
    prompt.textContent = item.request;
    const fileNames = item.sourceIds.map(id => sources.find(source => source.id === id)?.fileName)
      .filter(Boolean).join(", ");
    const files = document.createElement("div");
    files.className = "queue-status";
    files.textContent = fileNames || "File đã bị xóa";
    content.append(title, prompt, files);

    const actions = document.createElement("div");
    actions.className = "row";
    if (item.status === "error") {
      const retry = document.createElement("button");
      retry.type = "button";
      retry.className = "secondary";
      setIconButton(retry, "refresh", "Thử lại yêu cầu");
      retry.addEventListener("click", () => {
        item.status = "waiting";
        renderRequestQueue();
      });
      actions.append(retry);
    }
    if (item.result?.job) {
      const preview = document.createElement("button");
      preview.type = "button";
      preview.className = "secondary";
      setIconButton(preview, "eye", "Xem preview");
      preview.addEventListener("click", () => document.dispatchEvent(
        new CustomEvent("queued-result-selected", { detail: item })));
      actions.append(preview);
    }
    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "danger secondary";
    setIconButton(remove, "trash", "Xóa yêu cầu khỏi hàng đợi");
    remove.disabled = queueRunning && activeQueueItem === item;
    remove.addEventListener("click", () => removeQueueItem(item));
    actions.append(remove);
    row.append(content, actions);
    container.append(row);
  }

  const waiting = requestQueue.filter(item => item.status === "waiting").length;
  const needsAnswer = requestQueue.some(item => item.status === "needs-answer");
  const runLabel = queueRunning
    ? "AGY đang xử lý hàng đợi…"
    : !plannerReady ? "AGY chưa sẵn sàng"
    : needsAnswer ? "Cần trả lời câu hỏi của AGY"
    : waiting ? `Chạy hàng đợi (${waiting})` : "Không còn yêu cầu chờ";
  byId("queue-run-button").disabled = queueRunning || waiting === 0 || !plannerReady || needsAnswer;
  setIconButton(byId("queue-run-button"), queueRunning ? "refresh" : "play", runLabel);
  byId("queue-run-status").textContent = runLabel;
}

function queueStatusLabel(status) {
  return ({
    waiting: "đang chờ",
    processing: "AGY đang xử lý",
    "needs-answer": "cần bạn trả lời",
    done: "đã xong",
    error: "lỗi",
    cancelled: "đã hủy"
  })[status] || status;
}

function removeQueueItem(item) {
  if (queueRunning && activeQueueItem === item) return;
  requestQueue = requestQueue.filter(entry => entry !== item);
  requestQueue.forEach((entry, index) => entry.order = index + 1);
  if (activeQueueItem === item) {
    activeQueueItem = null;
    byId("planner-clarification").hidden = true;
  }
  renderRequestQueue();
}

function cancelRequestsForSources(sourceIds) {
  const removed = new Set(sourceIds);
  for (const item of requestQueue) {
    if (!item.sourceIds.some(id => removed.has(id))) continue;
    item.status = "cancelled";
    item.pendingQuestion = "";
    item.result = null;
    if (activeQueueItem === item) activeQueueItem = null;
  }
  byId("planner-clarification").hidden = true;
}

byId("planner-button").addEventListener("click", enqueuePlannerRequest);
byId("queue-run-button").addEventListener("click", () => runPlannerQueue());
