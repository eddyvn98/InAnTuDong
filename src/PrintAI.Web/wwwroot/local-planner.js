document.addEventListener("local-ready", loadPlannerStatus);
document.addEventListener("sources-updated", loadPlannerStatus);

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

byId("planner-button").addEventListener("click", async () => {
  const resultBox = byId("planner-result");
  const button = byId("planner-button");
  const sourceIds = [...byId("sources").querySelectorAll("input:checked")]
    .map(input => input.value);
  button.disabled = true;
  resultBox.textContent = "AGY đang phân tích yêu cầu…";
  try {
    const response = await authorizedFetch("/api/local/plan", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sourceIds,
        userRequest: byId("planner-request").value
      })
    });
    const result = await response.json();
    if (!result.job) {
      resultBox.textContent = `Cần làm rõ: ${result.questions.join(" · ")}`;
      return;
    }
    activeJob = result.job;
    previewReady = false;
    byId("pdf-button").disabled = false;
    updatePrintButton();
    byId("job-summary").textContent =
      `AGY ${result.tier || "planner"} · ${Math.round(result.confidence * 100)}% · ` +
      `${activeJob.itemCount} mục · ${activeJob.outputPageCount} trang A4`;
    resultBox.textContent = result.warnings?.length
      ? `Lưu ý: ${result.warnings.join(" · ")}`
      : "Đã lập kế hoạch. Kiểm tra preview trước khi xuất PDF hoặc in.";
    renderPageButtons();
    await showPreview(0);
  } catch (error) {
    resultBox.textContent = error.message;
  } finally {
    button.disabled = false;
  }
});
