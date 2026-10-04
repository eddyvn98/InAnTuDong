let sourcePreview = null;
let sourcePreviewRequest = 0;
let sourcePreviewThumbnailObserver = null;
let sourcePreviewPageIndex = 0;
let sourcePreviewActiveSet = false;
let sourcePreviewPageObserver = null;

function showSourcePreview(source) {
  sourcePreview = source;
  const requestId = ++sourcePreviewRequest;
  sourcePreviewThumbnailObserver?.disconnect();
  sourcePreviewPageObserver?.disconnect();
  sourcePreviewActiveSet = false;
  byId("source-preview-canvas").scrollTop = 0;
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
    : "1 trang";
  renderSources();

  if (pageCount > 1) renderSourceThumbnails(source, pageCount, requestId);
  renderSourcePreviewPages(source, pageCount, requestId);
  setActiveSourcePreviewPage(0, source);
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
  const page = byId("source-preview-pages").children[pageIndex];
  if (!page) return;
  const canvas = byId("source-preview-canvas");
  canvas.scrollTo({ top: sourcePreviewPageTop(canvas, page) - 14 });
  setActiveSourcePreviewPage(pageIndex, source);
}

function setActiveSourcePreviewPage(pageIndex, source) {
  if (pageIndex === sourcePreviewPageIndex && sourcePreviewActiveSet) return;
  sourcePreviewPageIndex = pageIndex;
  sourcePreviewActiveSet = true;
  const thumbnails = byId("source-preview-thumbnails");
  for (const button of thumbnails.children) {
    const selected = Number(button.dataset.pageIndex) === pageIndex;
    button.classList.toggle("active", selected);
    button.setAttribute("aria-pressed", String(selected));
    if (selected) button.scrollIntoView({ block: "nearest" });
  }
  byId("source-preview-status").textContent =
    `${pageCountLabel(source)} · đang xem trang ${pageIndex + 1}.`;
}

function sourcePreviewPageTop(canvas, page) {
  return page.getBoundingClientRect().top - canvas.getBoundingClientRect().top + canvas.scrollTop;
}

function renderSourcePreviewPages(source, pageCount, requestId) {
  const canvas = byId("source-preview-canvas");
  const container = byId("source-preview-pages");
  sourcePreviewPageObserver = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) loadSourcePreviewPage(entry.target, source, requestId);
      else releaseSourcePreviewPage(entry.target);
    }
  }, { root: canvas, rootMargin: "700px 0px" });

  const fragment = document.createDocumentFragment();
  for (let pageIndex = 0; pageIndex < pageCount; pageIndex++) {
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
    fragment.append(page);
  }
  container.append(fragment);
  for (const page of container.children) sourcePreviewPageObserver.observe(page);

  let frame = 0;
  canvas.onscroll = () => {
    if (frame) return;
    frame = requestAnimationFrame(() => {
      frame = 0;
      if (requestId !== sourcePreviewRequest) return;
      const probe = canvas.scrollTop + canvas.clientHeight / 3;
      let current = 0;
      for (const page of container.children)
        if (sourcePreviewPageTop(canvas, page) <= probe) current = Number(page.dataset.pageIndex);
      setActiveSourcePreviewPage(current, source);
    });
  };
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
    if (isStaleSourcePreview(page, source, requestId)) return;

    const image = new Image();
    image.className = "source-preview-image";
    image.alt = `Preview ${source.fileName}, trang ${pageIndex + 1}`;
    image.src = url;
    await image.decode();
    if (isStaleSourcePreview(page, source, requestId)) return;

    page.querySelector(".source-preview-page-content").replaceChildren(image);
    page.style.minHeight = "";
    page.dataset.previewUrl = url;
    url = "";
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

function isStaleSourcePreview(page, source, requestId) {
  return requestId !== sourcePreviewRequest || sourcePreview?.id !== source.id ||
    !page.isConnected;
}

function isStaleSourceThumbnail(button, source, requestId) {
  return requestId !== sourcePreviewRequest || sourcePreview?.id !== source.id ||
    !button.isConnected;
}

function releaseSourcePreviewPage(page) {
  if (!page.dataset.previewUrl) return;
  page.style.minHeight = `${page.offsetHeight}px`;
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
  sourcePreviewPageObserver?.disconnect();
  sourcePreviewPageObserver = null;
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
