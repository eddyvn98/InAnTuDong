# Local Web on macOS

## Scope

The cross-platform `PrintAI.Web` host provides a local browser workflow on macOS:

```text
JPG / PNG / HEIC / PDF upload
  -> source inspection
  -> optional AGY natural-language planning
  -> deterministic shared A4 layout
  -> page preview
  -> 300 DPI A4 PDF export or macOS CUPS print queue
```

This is a browser-first local utility, not a native desktop shell. When the AGY CLI is installed and signed in, the browser can send a natural-language request to the local Antigravity planner. PrintAI validates the returned print job, binds it to the uploaded-source allowlist, computes layout locally, and requires preview before export or printing. Mac printing submits the generated preview PDF through the system `lp` utility to a selected CUPS destination. Scanning remains on the Windows desktop path.

## Run

Install the .NET 10 SDK, then run:

```bash
dotnet run --project src/PrintAI.Web/PrintAI.Web.csproj
```

Open `http://127.0.0.1:5272/`. If that port is occupied, set `PRINTAI_WEB_PORT` to an available port. `PORT` is reserved for hosted demo environments and disables the Mac file workflow.

## File and job limits

- 100 files and 256 MB total per process session
- 100 MB per file; PDF sources support up to 200 pages each
- 1,000 source placements and 20 output pages per job
- supported uploads: JPG/JPEG, PNG, HEIC/HEIF and PDF
- uploads and generated previews/PDFs live under a random process-scoped directory in the OS temp folder; the directory is removed when the app exits
- PDF export rasterizes output pages at 300 DPI while preserving A4 physical page dimensions
- macOS printer destinations are discovered from CUPS; printing is enabled only after a successful preview and a destination is selected
- each print submission accepts 1–100 sets and rechecks that the selected destination still exists immediately before submitting
- this Mac currently has no CUPS destination configured; add one in System Settings → Printers & Scanners before sending a physical job

## Local API boundary

The Mac workflow is registered only for a loopback host without a hosting `PORT`. Mutating and file-returning APIs require the process-random `X-PrintAI-Session` token. Requests must come from a loopback connection and an expected loopback Host/Origin. CORS is not enabled, uploads use generated storage names, and the browser never supplies a filesystem path.

AGY is discovered from `PRINTAI_AGY_PATH`, `PATH`, or the current user's `~/.local/bin/agy`; it uses the existing CLI login and the configured fast/deep models. The request endpoint accepts uploaded-source IDs only. AGY is a planner: local validation and layout remain authoritative, policy forces a preview, and no print command is sent until the user explicitly submits a reviewed job.

The existing Railway demo remains on `index.html` and its generated sample endpoints.

## Printing on macOS

Add and verify a printer in System Settings → Printers & Scanners. Refresh the printer list in PrintAI, select the destination and number of sets, create/review the preview, then choose **Gửi tới máy in**. PrintAI generates the same A4 PDF used by export and sends it to CUPS with argument-safe process invocation. No arbitrary command or file path is accepted from the browser.
