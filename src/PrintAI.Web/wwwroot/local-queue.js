let nextQueueId = 1;

function enqueuePlannerRequest() {
  const request = byId("planner-request").value.trim();
  const sourceIds = [...selectedSourceIds];
  if (!sourceIds.length) return showError(new Error("Chọn ít nhất một file cho yêu cầu."));
  if (!request) return showError(new Error("Nhập yêu cầu in trước khi gửi."));

  requestQueue.push({
    id: nextQueueId,
    order: nextQueueId++,
    request,
    sourceIds,
    history: [],
    pendingQuestion: "",
    status: "waiting",
    result: null
  });
  byId("planner-request").value = "";
  updatePlannerRequestButton();
  byId("message").textContent = "";
  byId("planner-result").textContent = plannerReady
    ? "Đang gửi yêu cầu tới AGY…"
    : "Đã nhận yêu cầu. Đang chờ AGY kết nối…";
  renderRequestQueue();
  void runPlannerQueue();
  if (!plannerReady) void loadPlannerStatus();
  byId("planner-request").focus();
}

function renderRequestQueue() {
  const waiting = requestQueue.filter(item => item.status === "waiting").length;
  const processing = requestQueue.find(item => item.status === "processing");
  const needsAnswer = requestQueue.find(item => item.status === "needs-answer");
  const done = requestQueue.filter(item => item.status === "done").length;
  const errors = requestQueue.filter(item => item.status === "error").length;
  const summary = processing
    ? `Đang xử lý yêu cầu ${processing.order}. Còn ${waiting} yêu cầu chờ.`
    : needsAnswer
    ? `Yêu cầu ${needsAnswer.order} cần bạn trả lời để tiếp tục.`
    : !plannerReady && waiting
    ? `${waiting} yêu cầu đang chờ AGY kết nối.`
    : waiting
    ? `${waiting} yêu cầu đang chờ xử lý.`
    : `${done} yêu cầu hoàn tất${errors ? `, ${errors} yêu cầu lỗi` : ""}.`;
  byId("request-queue").textContent = requestQueue.length ? summary : "";
  renderSourceQueueBadges();
}

function queueStatusLabel(status) {
  return ({
    waiting: "đang chờ",
    processing: "đang xử lý",
    "needs-answer": "cần trả lời",
    done: "đã xong",
    error: "lỗi",
    cancelled: "đã hủy"
  })[status] || status;
}

function renderSourceQueueBadges() {
  const priority = { processing: 0, "needs-answer": 1, waiting: 2, error: 3, done: 4, cancelled: 5 };
  const shortLabels = {
    waiting: "chờ",
    processing: "đang xử lý",
    "needs-answer": "cần trả lời",
    done: "xong",
    error: "lỗi",
    cancelled: "đã hủy"
  };

  for (const card of byId("sources").querySelectorAll(".source-card")) {
    const sourceId = card.dataset.sourceId;
    const item = requestQueue
      .filter(entry => entry.sourceIds.includes(sourceId))
      .sort((a, b) => priority[a.status] - priority[b.status] || a.order - b.order)[0];
    const trigger = card.querySelector(".source-preview-trigger");
    const existing = trigger.querySelector(".source-queue-badge");
    if (!item) {
      existing?.remove();
      continue;
    }

    const badge = existing || document.createElement("span");
    badge.className = "source-queue-badge";
    badge.dataset.status = item.status;
    badge.textContent = `#${item.order} ${shortLabels[item.status] || item.status}`;
    badge.title = `Yêu cầu ${item.order} · ${queueStatusLabel(item.status)}`;
    badge.setAttribute("aria-label", badge.title);
    if (!existing) trigger.append(badge);
  }
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
  renderRequestQueue();
}

byId("planner-form").addEventListener("submit", event => {
  event.preventDefault();
  enqueuePlannerRequest();
});
byId("planner-request").addEventListener("input", updatePlannerRequestButton);
byId("planner-request").addEventListener("keydown", event => {
  if (event.key !== "Enter" || event.shiftKey || event.isComposing) return;
  event.preventDefault();
  byId("planner-form").requestSubmit();
});
document.addEventListener("local-ready", loadPlannerStatus);
document.addEventListener("sources-updated", loadPlannerStatus);
document.addEventListener("sources-updated", updatePlannerRequestButton);
document.addEventListener("selection-updated", updatePlannerRequestButton);
