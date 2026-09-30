const previewedSourceIds = new Set();
let sourcePreview = null;
let sourcePreviewRequest = 0;
let sourcePreviewThumbnailObserver = null;
let sourcePreviewPageVisibilityObserver = null;
let sourcePreviewPageLoaded = false;
let sourcePreviewPageVisible = false;
let sourcePreviewPageIndex = 0;

function showSourcePreview(source) {
  sourcePreview = source;
  const requestId = ++sourcePreviewRequest;
  sourcePreviewThumbnailObserver?.disconnect();
  sourcePreviewPageVisibilityObserver?.disconnect();
  sourcePreviewPageVisibilityObserver = null;
  sourcePreviewPageLoaded = false;
  sourcePreviewPageVisible = false;
  const pages = byId("source-preview-pages");
  const thumbnails = byId("source-preview-thumbnails");
  for (const page of pages.children) releaseSourcePreviewPage(page);
  for (const thumbnail of thumbnails.children) releaseSourcePreviewThumbnail(thumbnail);
  pages.replaceChildren();
  thumbnails.replaceChildren();

  const pageCount = Math.max(1, source.pageCount || 1);
  const layout = byId("source-preview-layout");
  layout.classList.toggle("has-thumbnails", pageCount > 1);
  thumbnails.hidden = pageCount <= 1;
  byId("source-preview-name").textContent = source.fileName;
  byId("source-preview-status").textContent = pageCount > 1
    ? `${pageCount} trang · chọn thumbnail để xem nhanh.`
    : "Đang tải preview bản gốc…";
  renderSources();

  if (pageCount > 1) renderSourceThumbnails(source, pageCount, requestId);
  showSourcePreviewPage(0, source, requestId);
}

function renderSourceThumbnails(source, pageCount, requestId) {
  const container = byId("source-preview-thumbnails");
  sourcePreviewThumbnailObserver = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting)
        loadSourcePreviewThumbnail(entry.target, source, requestId);
      else releaseSourcePreviewThumbnail(entry.target);
    }
  }, { root: container, rootMargin: "180px 0px" });

  const fragment = document.createDocumentFragment();
  for (let pageIndex = 0; pageIndex < pageCount; pageIndex++) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "source-page-thumbnail";
    button.dataset.pageIndex = String(pageIndex);
    button.setAttribute("aria-label", `Xem trang ${pageIndex + 1}`);
    button.setAttribute("aria-pressed", String(pageIndex === 0));
    const image = document.createElement("span");
    image.className = "source-page-thumbnail-image";
    image.textContent = "…";
    const label = document.createElement("span");
    label.className = "source-page-thumbnail-label";
    label.textContent = `Trang ${pageIndex + 1}`;
    button.append(image, label);
    button.addEventListener("click", () =>
      showSourcePreviewPage(pageIndex, source, requestId));
    fragment.append(button);
  }
  container.append(fragment);
  for (const button of container.children) sourcePreviewThumbnailObserver.observe(button);
}

function showSourcePreviewPage(pageIndex, source, requestId) {
  if (requestId !== sourcePreviewRequest || sourcePreview?.id !== source.id) return;
  sourcePreviewPageIndex = pageIndex;
  sourcePreviewPageLoaded = false;
  sourcePreviewPageVisible = false;
  sourcePreviewPageVisibilityObserver?.disconnect();

  const pages = byId("source-preview-pages");
  for (const page of pages.children) releaseSourcePreviewPage(page);
  pages.replaceChildren();
  const page = document.createElement("article");
  page.className = "source-preview-page";
  page.dataset.pageIndex = String(pageIndex);
  const label = document.createElement("span");
  label.className = "source-preview-page-label";
  label.textContent = `Trang ${pageIndex + 1}`;
  const content = document.createElement("div");
  content.className = "source-preview-page-content";
  content.textContent = "Đang tải preview trang…";
  page.append(label, content);
  pages.append(page);
  byId("source-preview-canvas").scrollTop = 0;

  const thumbnails = byId("source-preview-thumbnails");
  for (const button of thumbnails.children) {
    const selected = Number(button.dataset.pageIndex) === pageIndex;
    button.classList.toggle("active", selected);
    button.setAttribute("aria-pressed", String(selected));
  }

  sourcePreviewPageVisibilityObserver = new IntersectionObserver(entries => {
    sourcePreviewPageVisible = entries.some(entry => entry.isIntersecting);
    markSourcePreviewViewed(source, requestId);
  }, { root: byId("source-preview-canvas"), threshold: 0.05 });
  sourcePreviewPageVisibilityObserver.observe(page);
  loadSourcePreviewPage(page, source, requestId);
}

async function loadSourcePreviewPage(page, source, requestId) {
  if (page.dataset.loading || page.dataset.previewUrl) return;
  page.dataset.loading = "true";
  let url = "";
  try {
    const pageIndex = Number(page.dataset.pageIndex);
    const response = await authorizedFetch(
      `/api/local/sources/${source.id}/preview/${pageIndex}`);
    url = URL.createObjectURL(await response.blob());
    if (isStaleSourcePreview(page, source, requestId, pageIndex)) return;

    const image = new Image();
    image.className = "source-preview-image";
    image.alt = `Preview ${source.fileName}, trang ${pageIndex + 1}`;
    image.src = url;
    await image.decode();
    if (isStaleSourcePreview(page, source, requestId, pageIndex)) return;

    page.querySelector(".source-preview-page-content").replaceChildren(image);
    page.dataset.previewUrl = url;
    url = "";
    sourcePreviewPageLoaded = true;
    byId("source-preview-status").textContent =
      `${pageCountLabel(source)} · đang xem trang ${pageIndex + 1}.`;
    markSourcePreviewViewed(source, requestId);
  } catch (error) {
    if (requestId === sourcePreviewRequest && page.isConnected)
      page.querySelector(".source-preview-page-content").textContent =
        `Không tải được trang xem trước: ${error.message}`;
  } finally {
    if (url) URL.revokeObjectURL(url);
    delete page.dataset.loading;
  }
}

async function loadSourcePreviewThumbnail(button, source, requestId) {
  if (button.dataset.loading || button.dataset.previewUrl) return;
  button.dataset.loading = "true";
  let url = "";
  try {
    const pageIndex = Number(button.dataset.pageIndex);
    const response = await authorizedFetch(
      `/api/local/sources/${source.id}/preview/${pageIndex}`);
    url = URL.createObjectURL(await response.blob());
    if (isStaleSourceThumbnail(button, source, requestId)) return;

    const image = new Image();
    image.alt = "";
    image.src = url;
    await image.decode();
    if (isStaleSourceThumbnail(button, source, requestId)) return;

    const frame = button.querySelector(".source-page-thumbnail-image");
    frame.replaceChildren(image);
    button.dataset.previewUrl = url;
    url = "";
  } catch (error) {
    const frame = button.querySelector(".source-page-thumbnail-image");
    if (requestId === sourcePreviewRequest && button.isConnected)
      frame.textContent = "!";
  } finally {
    if (url) URL.revokeObjectURL(url);
    delete button.dataset.loading;
  }
}

function markSourcePreviewViewed(source, requestId) {
  if (requestId !== sourcePreviewRequest || !sourcePreviewPageLoaded || !sourcePreviewPageVisible)
    return;
  previewedSourceIds.add(source.id);
  updatePlannerRequestButton();
}

function isStaleSourcePreview(page, source, requestId, pageIndex) {
  return requestId !== sourcePreviewRequest || sourcePreview?.id !== source.id ||
    sourcePreviewPageIndex !== pageIndex || !page.isConnected;
}

function isStaleSourceThumbnail(button, source, requestId) {
  return requestId !== sourcePreviewRequest || sourcePreview?.id !== source.id ||
    !button.isConnected;
}

function releaseSourcePreviewPage(page) {
  if (!page.dataset.previewUrl) return;
  URL.revokeObjectURL(page.dataset.previewUrl);
  delete page.dataset.previewUrl;
  page.querySelector(".source-preview-page-content").textContent =
    "Đang tải preview trang…";
}

function releaseSourcePreviewThumbnail(button) {
  if (!button.dataset.previewUrl) return;
  URL.revokeObjectURL(button.dataset.previewUrl);
  delete button.dataset.previewUrl;
  const frame = button.querySelector(".source-page-thumbnail-image");
  if (frame) frame.textContent = "…";
}

function pageCountLabel(source) {
  return `${source.pageCount} trang`;
}

function clearSourcePreview() {
  sourcePreviewRequest++;
  sourcePreview = null;
  sourcePreviewThumbnailObserver?.disconnect();
  sourcePreviewThumbnailObserver = null;
  sourcePreviewPageVisibilityObserver?.disconnect();
  sourcePreviewPageVisibilityObserver = null;
  sourcePreviewPageLoaded = false;
  sourcePreviewPageVisible = false;
  const pages = byId("source-preview-pages");
  const thumbnails = byId("source-preview-thumbnails");
  for (const page of pages.children) releaseSourcePreviewPage(page);
  for (const thumbnail of thumbnails.children) releaseSourcePreviewThumbnail(thumbnail);
  pages.replaceChildren();
  thumbnails.replaceChildren();
  thumbnails.hidden = true;
  byId("source-preview-layout").classList.remove("has-thumbnails");
  byId("source-preview-name").textContent = "Chưa có file";
  byId("source-preview-status").textContent = "Chọn file để xem";
}
