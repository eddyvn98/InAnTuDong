# Local Web (Windows loopback)

## Goal

Run PrintAI's browser UI on the same Windows machine that owns:

- the authenticated Antigravity CLI session
- local source files uploaded by the browser
- installed printer/scanner drivers
- Windows spooler access

The production local surface is loopback-only. Railway remains demo/test only.

## Address

When `PrintAI.exe` starts successfully, it also starts:

```text
http://127.0.0.1:5271/
```

The desktop UI includes **Mở web local** to open this address in the default browser.

Closing `PrintAI.exe` stops the local web host.

## Core browser flow in this slice

```text
browser
  -> upload source
  -> PrintAI local temp workspace
  -> shared DesktopSession
  -> source inspection
  -> AGY fast/deep planner
  -> deterministic validation/compiler
  -> preview
  -> Windows spooler
```

Supported browser operations in the first slice:

- upload JPG/JPEG/PNG/HEIC/HEIF/PDF/Office files
- view loaded files/pages
- select source page
- select printer
- submit natural-language planning request
- select PrintPlan batch
- select preview output page
- print current output
- print current job/batch
- print an all-simplex PrintPlan
- continue/cancel manual duplex
- clear sources
- refresh AGY readiness

Scanner, recipes, collage controls and advanced composition remain in the desktop WebView2 UI until later migration slices.

## File handling

A browser cannot safely hand PrintAI an arbitrary native filesystem path.

Uploaded files are therefore copied into a process-scoped workspace below:

```text
%LOCALAPPDATA%\PrintAI\local-web\
```

The workspace is deleted best-effort when the desktop process closes.

The local API currently accepts at most:

- 100 files per upload request
- 100 MB per individual file
- 256 MB total request body

Only PrintAI-supported extensions are accepted.

Office conversion still uses the existing isolated conversion pipeline after upload.

## Security boundary

The local server is not a LAN server.

Controls:

1. Kestrel binds only to `127.0.0.1`.
2. Requests are rejected unless the remote address is loopback.
3. Host headers other than `127.0.0.1` or `localhost` are rejected.
4. CORS is not enabled.
5. `/api/bootstrap` creates/returns a random process-local session token.
6. State-changing APIs require that token in `X-PrintAI-Session`.
7. Browser uploads are copied into a controlled local workspace.
8. The API exposes no arbitrary shell endpoint and no direct arbitrary-path read endpoint.
9. AGY remains planning-only; deterministic PrintAI code owns print execution.

The random token is regenerated on every process start and is not persisted.

## Threading

The local ASP.NET Core server handles network requests on normal server threads, but `DesktopSession` was originally designed for the WPF application.

`LocalWebSession` therefore:

- serializes browser operations with a semaphore;
- marshals DesktopSession operations onto the WPF Dispatcher;
- uses the same in-process session as the desktop UI.

This avoids allowing Kestrel request threads to directly mutate printer/session state.

## Endpoints

```text
GET  /health
GET  /api/bootstrap
GET  /api/state
POST /api/files
POST /api/action
```

`/api/files` uses multipart form data.

`/api/action` accepts a small allowlisted action contract; it is not a generic command or shell endpoint.

## Next slices

1. verify the packaged app on the target Windows machine through a real browser;
2. migrate recipes/scanner/workflow controls that are useful in browser mode;
3. add browser-first startup mode so the WPF surface can become optional;
4. measure AGY cold/warm latency from the browser path;
5. add persistent AGY `stream-json` only if measurements justify it.
