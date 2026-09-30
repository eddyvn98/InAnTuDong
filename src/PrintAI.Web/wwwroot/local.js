let sessionToken = "";
let sources = [];
let activeJob = null;
let previewUrl = "";
let previewReady = false;
let printers = [];
let requestQueue = [];
let activeQueueItem = null;
let queueRunning = false;

const byId = id => document.getElementById(id);
const message = byId("message");

function setIconButton(button, icon, label) {
  const image = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
  image.setAttribute("aria-hidden", "true");
  image.setAttribute("focusable", "false");
  use.setAttribute("href", `/icons.svg?v=source-preview-v1#${icon}`);
  image.append(use);
  button.replaceChildren(image);
  button.classList.add("icon-only");
  button.setAttribute("aria-label", label);
  button.title = label;
}

async function authorizedFetch(url, options = {}) {
  const headers = new Headers(options.headers || {});
  headers.set("X-PrintAI-Session", sessionToken);
  const response = await fetch(url, { ...options, headers });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const error = new Error(body?.error || `Yêu cầu thất bại (${response.status}).`);
    error.status = response.status;
    throw error;
  }
  return response;
}

function showError(error) {
  message.textContent = error instanceof Error ? error.message : String(error);
}

function setPreviewMode(mode) {
  const showPrint = mode === "print" && previewReady;
  byId("source-preview-panel").hidden = showPrint;
  byId("print-preview-panel").hidden = !showPrint;
  byId("source-preview-tab").classList.toggle("active", !showPrint);
  byId("source-preview-tab").setAttribute("aria-pressed", String(!showPrint));
  byId("source-preview-tab").disabled = sources.length === 0;
  byId("print-preview-tab").classList.toggle("active", showPrint);
  byId("print-preview-tab").setAttribute("aria-pressed", String(showPrint));
  byId("print-preview-tab").disabled = !previewReady;
}

async function initialize() {
  const response = await fetch("/api/bootstrap");
  if (!response.ok) throw new Error("Mở web bằng địa chỉ local đã in ra trong Terminal.");
  const bootstrap = await response.json();
  sessionToken = bootstrap.sessionToken;
  const sourceResponse = await authorizedFetch("/api/local/sources");
  sources = await sourceResponse.json();
  selectedSourceIds = new Set(sources.map(source => source.id));
  renderSources(true);
  await loadPrinters();
  byId("status").textContent = "Local · đã kết nối";
  byId("status").dataset.state = "success";
  document.dispatchEvent(new Event("local-ready"));
}

function renderPageButtons() {
  const pages = byId("pages");
  pages.replaceChildren();
  for (let page = 0; page < activeJob.outputPageCount; page++) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "secondary";
    setIconButton(button, "file", `Xem trang ${page + 1}`);
    button.addEventListener("click", () => showPreview(page));
    pages.append(button);
  }
}

async function showPreview(page) {
  try {
    const response = await authorizedFetch(`/api/local/jobs/${activeJob.id}/preview/${page}`);
    const nextUrl = URL.createObjectURL(await response.blob());
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    previewUrl = nextUrl;
    byId("preview").src = previewUrl;
    byId("preview").hidden = false;
    await byId("preview").decode();
    previewReady = true;
    updatePrintButton();
    setPreviewMode("print");
    byId("pages").querySelectorAll("button").forEach((button, index) => {
      button.classList.toggle("active", index === page);
    });
  } catch (error) {
    showError(error);
  }
}

function updatePrintButton() {
  byId("print-button").disabled = !activeJob || !previewReady || printers.length === 0;
  byId("print-preview-tab").disabled = !previewReady;
}

async function loadPrinters() {
  const select = byId("printer-select");
  const note = byId("printer-message");
  byId("printer-refresh").disabled = true;
  select.disabled = true;
  try {
    const response = await authorizedFetch("/api/local/printers");
    const result = await response.json();
    printers = result.printers || [];
    select.replaceChildren();
    for (const printer of printers) {
      const option = document.createElement("option");
      option.value = printer.name;
      option.textContent = `${printer.name}${printer.isDefault ? " · mặc định" : ""} · ${printer.status}`;
      select.append(option);
    }
    if (!printers.length) {
      select.append(new Option("Chưa có máy in", ""));
    } else if (printers.some(printer => printer.isDefault)) {
      select.value = printers.find(printer => printer.isDefault).name;
    }
    select.disabled = printers.length === 0;
    byId("print-copies").disabled = printers.length === 0;
    note.textContent = result.message || "PDF preview sẽ được gửi vào hàng đợi in hệ thống.";
    updatePrintButton();
  } catch (error) {
    printers = [];
    select.replaceChildren(new Option("Không đọc được danh sách máy in", ""));
    note.textContent = error.message;
    updatePrintButton();
  } finally {
    byId("printer-refresh").disabled = false;
  }
}

byId("printer-refresh").addEventListener("click", loadPrinters);
byId("source-preview-tab").addEventListener("click", () => setPreviewMode("source"));
byId("print-preview-tab").addEventListener("click", () => setPreviewMode("print"));
document.addEventListener("sources-updated", () => {
  byId("source-preview-tab").disabled = sources.length === 0;
  if (sources.length) setPreviewMode("source");
});
byId("print-button").addEventListener("click", async () => {
  if (!activeJob || !previewReady || !byId("printer-select").value) return;
  byId("print-button").disabled = true;
  message.textContent = "Đang gửi PDF vào hàng đợi in…";
  try {
    const response = await authorizedFetch(`/api/local/jobs/${activeJob.id}/print`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        printerName: byId("printer-select").value,
        copies: Number(byId("print-copies").value)
      })
    });
    const result = await response.json();
    message.textContent = `Đã gửi ${result.copies} bộ tới ${result.printerName}. ${result.message}`;
  } catch (error) {
    showError(error);
  } finally {
    updatePrintButton();
  }
});

byId("pdf-button").addEventListener("click", async () => {
  if (!activeJob) return;
  message.textContent = "Đang tạo PDF 300 DPI…";
  byId("pdf-button").disabled = true;
  try {
    const response = await authorizedFetch(`/api/local/jobs/${activeJob.id}/pdf`);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = "PrintAI-A4.pdf";
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
    message.textContent = "Đã tạo PDF. Mở tệp để xem hoặc gửi tới máy in.";
  } catch (error) {
    showError(error);
  } finally {
    byId("pdf-button").disabled = false;
  }
});

initialize().catch(error => {
  byId("status").textContent = "Chưa kết nối";
  byId("status").dataset.state = "error";
  showError(error);
});
