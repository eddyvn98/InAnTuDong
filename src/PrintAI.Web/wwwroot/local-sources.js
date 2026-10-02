let selectedSourceIds = new Set();
const sourceThumbnailUrls = new Map();
const sourceThumbnailLoads = new Set();
let pendingRemovalIds = [];

function canSubmitPlannerRequest() {
  return selectedSourceIds.size === 0 ||
    [...selectedSourceIds].every(sourceId => previewedSourceIds.has(sourceId));
}

function updatePlannerRequestButton() {
  const ready = canSubmitPlannerRequest();
  const button = byId("planner-button");
  const requirement = byId("source-preview-requirement");
  button.disabled = !plannerReady || !ready;
  const label = !selectedSourceIds.size
    ? "Gửi yêu cầu cho AI, không bắt buộc có file"
    : ready ? "Thêm vào hàng đợi AGY"
    : "Rà soát preview các file đã chọn trước khi gửi yêu cầu";
  button.setAttribute("aria-label", label);
  button.title = label;
  requirement.hidden = false;
  requirement.textContent = selectedSourceIds.size === 0
    ? "Không có file: AI có thể tạo nguồn in từ mô tả của bạn."
    : ready
    ? "Đã xem preview các file đã chọn. Bạn có thể gửi yêu cầu."
    : "Mở preview bản gốc ở bên phải. Chọn thumbnail khác để rà soát từng file trước khi gửi yêu cầu.";
}

function renderSources(sourcesChanged = false) {
  const container = byId("sources");
  container.replaceChildren();
  for (const source of sources) {
    const card = document.createElement("div");
    card.className = `source-card${selectedSourceIds.has(source.id) ? " selected" : ""}${sourcePreview?.id === source.id ? " viewing" : ""}`;
    card.dataset.sourceId = source.id;

    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.className = "source-check";
    checkbox.checked = selectedSourceIds.has(source.id);
    checkbox.setAttribute("aria-label", `Chọn ${source.fileName} cho yêu cầu`);

    const thumbnail = document.createElement("div");
    thumbnail.className = "source-thumb";
    thumbnail.textContent = source.kind === "Pdf" ? "PDF" : "Ảnh";
    const cachedUrl = sourceThumbnailUrls.get(source.id);
    if (cachedUrl) {
      const image = document.createElement("img");
      image.src = cachedUrl;
      image.alt = `Thumbnail ${source.fileName}`;
      thumbnail.replaceChildren(image);
    } else {
      loadSourceThumbnail(source);
    }

    const previewTrigger = document.createElement("button");
    previewTrigger.type = "button";
    previewTrigger.className = "source-preview-trigger";
    previewTrigger.setAttribute("aria-label", `Xem preview ${source.fileName}`);
    previewTrigger.title = `Xem preview ${source.fileName}`;
    previewTrigger.setAttribute("aria-pressed", String(sourcePreview?.id === source.id));
    previewTrigger.append(thumbnail);
    previewTrigger.addEventListener("click", () => showSourcePreview(source));

    const name = document.createElement("div");
    name.className = "source-name";
    name.title = source.fileName;
    name.textContent = source.fileName;
    const meta = document.createElement("div");
    meta.className = "source-meta";
    meta.textContent = source.pageCount > 1 ? `${source.pageCount} trang` : source.kind;

    card.append(checkbox, previewTrigger, name, meta);
    container.append(card);
  }

  const selectedCount = selectedSourceIds.size;
  byId("upload-status").textContent = sources.length
    ? `${sources.length} file trong phiên · đã chọn ${selectedCount} file cho yêu cầu`
    : "Chọn xong, file sẽ tự tải lên và hiện tại đây.";
  byId("remove-sources-button").disabled = selectedCount === 0 || queueRunning;
  if (sourcesChanged && (!sourcePreview || !sources.some(source => source.id === sourcePreview.id))) {
    const nextPreview = sources.find(source => selectedSourceIds.has(source.id)) || sources[0];
    if (nextPreview) showSourcePreview(nextPreview);
    else clearSourcePreview();
  }
  document.dispatchEvent(new Event(sourcesChanged ? "sources-updated" : "selection-updated"));
}

async function loadSourceThumbnail(source) {
  if (sourceThumbnailLoads.has(source.id)) return;
  sourceThumbnailLoads.add(source.id);
  try {
    const response = await authorizedFetch(`/api/local/sources/${source.id}/thumbnail`);
    const url = URL.createObjectURL(await response.blob());
    if (!sources.some(item => item.id === source.id)) {
      URL.revokeObjectURL(url);
      return;
    }
    sourceThumbnailUrls.set(source.id, url);
    const card = [...byId("sources").children].find(item => item.dataset.sourceId === source.id);
    if (!card) return;
    const image = document.createElement("img");
    image.src = url;
    image.alt = `Thumbnail ${source.fileName}`;
    card.querySelector(".source-thumb").replaceChildren(image);
  } catch {
    // Keep the file-type placeholder when a thumbnail cannot be rendered.
  } finally {
    sourceThumbnailLoads.delete(source.id);
  }
}

async function uploadSelectedFiles(fileList) {
  const files = [...fileList];
  if (!files.length) return;
  byId("message").textContent = "";
  byId("upload-status").textContent = `Đang tải ${files.length} file lên…`;
  const form = new FormData();
  for (const file of files) form.append("files", file);
  byId("files-input").value = "";
  try {
    const response = await authorizedFetch("/api/local/files", { method: "POST", body: form });
    const added = await response.json();
    sources.push(...added);
    for (const source of added) selectedSourceIds.add(source.id);
    renderSources(true);
  } catch (error) {
    byId("upload-status").textContent = "Không tải được file.";
    showError(error);
  }
}

async function removeSelectedSources() {
  const ids = [...selectedSourceIds];
  if (!ids.length || queueRunning) return;
  const affected = requestQueue.filter(item =>
    item.status !== "cancelled" && item.sourceIds.some(id => ids.includes(id)));
  const suffix = affected.length
    ? ` ${affected.length} yêu cầu trong hàng đợi dùng các file này cũng sẽ bị hủy.`
    : "";
  pendingRemovalIds = ids;
  byId("source-delete-text").textContent = `Xóa ${ids.length} file khỏi phiên local?${suffix}`;
  byId("source-delete-confirm").hidden = false;
  byId("remove-sources-button").disabled = true;
}

async function confirmRemoveSelectedSources() {
  const ids = pendingRemovalIds;
  if (!ids.length || queueRunning) return;
  try {
    const response = await authorizedFetch("/api/local/sources", {
      method: "DELETE",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sourceIds: ids })
    });
    const removed = await response.json();
    cancelRequestsForSources(ids);
    for (const id of ids) {
      selectedSourceIds.delete(id);
      previewedSourceIds.delete(id);
      const thumbnailUrl = sourceThumbnailUrls.get(id);
      if (thumbnailUrl) URL.revokeObjectURL(thumbnailUrl);
      sourceThumbnailUrls.delete(id);
    }
    sources = sources.filter(source => !ids.includes(source.id));
    if (activeJob && removed.removedJobIds.includes(activeJob.id)) {
      activeJob = null;
      previewReady = false;
      byId("pdf-button").disabled = true;
      byId("preview").hidden = true;
      byId("preview").removeAttribute("src");
      byId("pages").replaceChildren();
      byId("job-summary").textContent = "Đã xóa file của preview hiện tại.";
      updatePrintButton();
    }
    renderSources(true);
    renderRequestQueue();
    pendingRemovalIds = [];
    byId("source-delete-confirm").hidden = true;
  } catch (error) {
    showError(error);
  } finally {
    byId("remove-sources-button").disabled = selectedSourceIds.size === 0 || queueRunning;
  }
}

function cancelRemoveSelectedSources() {
  pendingRemovalIds = [];
  byId("source-delete-confirm").hidden = true;
  byId("remove-sources-button").disabled = selectedSourceIds.size === 0 || queueRunning;
}

byId("files-input").addEventListener("change", event => uploadSelectedFiles(event.target.files));
byId("upload-form").addEventListener("dragover", event => {
  event.preventDefault();
  event.dataTransfer.dropEffect = "copy";
});
byId("upload-form").addEventListener("drop", event => {
  event.preventDefault();
  uploadSelectedFiles(event.dataTransfer.files);
});
byId("remove-sources-button").addEventListener("click", removeSelectedSources);
byId("source-delete-confirm-button").addEventListener("click", confirmRemoveSelectedSources);
byId("source-delete-cancel-button").addEventListener("click", cancelRemoveSelectedSources);
byId("sources").addEventListener("change", event => {
  const checkbox = event.target.closest(".source-check");
  if (!checkbox) return;
  const sourceId = checkbox.closest(".source-card").dataset.sourceId;
  if (checkbox.checked) selectedSourceIds.add(sourceId);
  else selectedSourceIds.delete(sourceId);
  renderSources();
});

const dropZone = byId("file-drop-zone");
if (dropZone) {
  for (const eventName of ["dragenter", "dragover"]) {
    dropZone.addEventListener(eventName, event => {
      event.preventDefault();
      dropZone.classList.add("drag-active");
    });
  }
  for (const eventName of ["dragleave", "drop"]) {
    dropZone.addEventListener(eventName, () => dropZone.classList.remove("drag-active"));
  }
  dropZone.addEventListener("drop", event => {
    event.preventDefault();
    event.stopPropagation();
    uploadSelectedFiles(event.dataTransfer.files);
  });
  dropZone.addEventListener("click", () => byId("files-input").click());
  dropZone.addEventListener("keydown", event => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      byId("files-input").click();
    }
  });
}
