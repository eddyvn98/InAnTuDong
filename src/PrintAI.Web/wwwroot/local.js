let sessionToken = "";
let sources = [];
let activeJob = null;
let previewUrl = "";
let previewReady = false;
let printers = [];

const byId = id => document.getElementById(id);
const message = byId("message");

async function authorizedFetch(url, options = {}) {
  const headers = new Headers(options.headers || {});
  headers.set("X-PrintAI-Session", sessionToken);
  const response = await fetch(url, { ...options, headers });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.error || `Yêu cầu thất bại (${response.status}).`);
  }
  return response;
}

function showError(error) {
  message.textContent = error instanceof Error ? error.message : String(error);
}

function renderSources() {
  const container = byId("sources");
  container.replaceChildren();
  for (const source of sources) {
    const label = document.createElement("label");
    label.className = "source";
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.value = source.id;
    checkbox.checked = true;
    const description = document.createElement("span");
    description.textContent = `${source.fileName} · ${source.pageCount} trang${source.pixelWidth ? ` · ${source.pixelWidth}×${source.pixelHeight}px` : ""}`;
    label.append(checkbox, description);
    container.append(label);
  }
  byId("job-form").hidden = sources.length === 0;
  byId("preview-button").disabled = sources.length === 0;
  document.dispatchEvent(new Event("sources-updated"));
}

async function initialize() {
  const response = await fetch("/api/bootstrap");
  if (!response.ok) throw new Error("Mở web bằng địa chỉ local đã in ra trong Terminal.");
  const bootstrap = await response.json();
  sessionToken = bootstrap.sessionToken;
  const sourceResponse = await authorizedFetch("/api/local/sources");
  sources = await sourceResponse.json();
  renderSources();
  await loadPrinters();
  byId("status").textContent = "Local · đã kết nối";
  byId("status").style.background = "#e8fff1";
  document.dispatchEvent(new Event("local-ready"));
}

byId("upload-form").addEventListener("submit", async event => {
  event.preventDefault();
  message.textContent = "";
  const form = new FormData();
  for (const file of byId("files-input").files) form.append("files", file);
  byId("upload-button").disabled = true;
  try {
    const response = await authorizedFetch("/api/local/files", { method: "POST", body: form });
    sources.push(...await response.json());
    renderSources();
    byId("files-input").value = "";
  } catch (error) {
    showError(error);
  } finally {
    byId("upload-button").disabled = false;
  }
});

byId("job-form").addEventListener("submit", async event => {
  event.preventDefault();
  message.textContent = "";
  const form = new FormData(event.currentTarget);
  const sourceIds = [...byId("sources").querySelectorAll("input:checked")].map(input => input.value);
  byId("preview-button").disabled = true;
  try {
    const response = await authorizedFetch("/api/local/jobs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sourceIds,
        itemWidthMm: Number(form.get("width")),
        itemHeightMm: Number(form.get("height")),
        copies: Number(form.get("copies")),
        gapMm: Number(form.get("gap")),
        marginMm: Number(form.get("margin"))
      })
    });
    activeJob = await response.json();
    previewReady = false;
    byId("pdf-button").disabled = false;
    updatePrintButton();
    byId("job-summary").textContent = `${activeJob.itemCount} mục · ${activeJob.columns}×${activeJob.rows}/trang · ${activeJob.outputPageCount} trang A4${activeJob.rotated ? " · xoay 90°" : ""}`;
    renderPageButtons();
    await showPreview(0);
  } catch (error) {
    showError(error);
  } finally {
    byId("preview-button").disabled = false;
  }
});

byId("job-form").addEventListener("input", () => {
  if (!activeJob) return;
  activeJob = null;
  previewReady = false;
  byId("pdf-button").disabled = true;
  byId("job-summary").textContent = "Thiết lập đã đổi. Tạo preview mới trước khi xuất hoặc in.";
  updatePrintButton();
});

function renderPageButtons() {
  const pages = byId("pages");
  pages.replaceChildren();
  for (let page = 0; page < activeJob.outputPageCount; page++) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "secondary";
    button.textContent = `Trang ${page + 1}`;
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
    previewReady = true;
    updatePrintButton();
    byId("pages").querySelectorAll("button").forEach((button, index) => {
      button.classList.toggle("active", index === page);
    });
  } catch (error) {
    showError(error);
  }
}

function updatePrintButton() {
  byId("print-button").disabled = !activeJob || !previewReady || printers.length === 0;
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
    note.textContent = result.message || "PDF preview sẽ được gửi vào hàng đợi CUPS của macOS.";
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
    message.textContent = "Đã tạo PDF. Mở tệp để xem hoặc gửi tới máy in Mac.";
  } catch (error) {
    showError(error);
  } finally {
    byId("pdf-button").disabled = false;
  }
});

initialize().catch(error => {
  byId("status").textContent = "Chưa kết nối";
  showError(error);
});
