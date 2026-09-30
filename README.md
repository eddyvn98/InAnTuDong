# Print AI / InAnTuDong

Windows-first AI-assisted printing system.

The product goal is simple: give the app file path(s) plus a natural-language request; it plans, validates, lays out, previews when needed, and prints without making the user navigate Epson software.

## Start here

Every implementation session must read:

1. `AGENTS.md`
2. `docs/PROGRESS.md`
3. `docs/ROADMAP.md`
4. relevant specs in `docs/`

The repository, not chat history, is the source of truth.

## Architecture

```
files + request
  -> source inspection
  -> Antigravity fast planner
       -> accept valid/confident JSON
       -> otherwise escalate once to Antigravity deep planner
  -> PrintJobSpec / PrintPlan 2.0
  -> validator
  -> deterministic layout
  -> preview
  -> policy gate
  -> Windows print adapter
  -> installed printer driver / spooler
```

Epson L3310 is the first hardware profile, not a hard-coded architecture dependency.

## Current deliverables

- deterministic A4 geometry core
- grid/repeat layout with optional 90-degree rotation
- validation
- cross-platform web demo for Railway
- GitHub Actions CI on Windows + Linux

Run locally:

```bash
dotnet run --project src/PrintAI.Web/PrintAI.Web.csproj
```

Then open `http://127.0.0.1:5272/`. Local runs bind to loopback; deployments with `PORT` set bind to `0.0.0.0` for the hosting platform. Set `PRINTAI_WEB_HOST` to override the bind address.

On macOS, install the .NET 10 SDK and run the same command in a new terminal. The local browser workflow accepts JPG/JPEG/PNG/HEIC/HEIF/PDF files, builds a deterministic A4 layout, shows each output page, exports a 300 DPI A4 PDF and sends it to a selected macOS CUPS printer. When AGY is installed and signed in, it can also turn a natural-language request into a validated print plan. The app binds to loopback and removes uploaded files when the server closes. Set `PRINTAI_WEB_PORT` if port 5272 is occupied. Add a printer in System Settings → Printers & Scanners before printing. Scanning remains a Windows desktop feature. See [Local Web on macOS](docs/LOCAL_MAC_WEB.md).

Windows desktop shell:

```powershell
dotnet run --project src/PrintAI.Desktop/PrintAI.Desktop.csproj
```

The desktop app supports JPG/PNG/PDF preview and printing, multi-page sources, AI-assisted PrintJobSpec planning, deterministic user edits, Windows printer submission and local job history.

While `PrintAI.exe` is running it also hosts the core browser workflow at:

```text
http://127.0.0.1:5271/
```

The local web uses the same in-process session, AGY planner and Windows printer path. See `docs/LOCAL_WEB.md`.

AI planning uses the locally installed and authenticated Antigravity CLI (`agy.exe`). PrintAI does not require an AI API key.

Optional non-secret tuning:

```text
PRINTAI_AGY_PATH
PRINTAI_AGY_FAST_MODEL
PRINTAI_AGY_DEEP_MODEL
PRINTAI_AGY_FAST_EFFORT
PRINTAI_AGY_DEEP_EFFORT
PRINTAI_AGY_ESCALATE_BELOW
PRINTAI_AGY_TIMEOUT
```

See `docs/ANTIGRAVITY_PLANNER.md`, `docs/AI_PLANNER.md` and `docs/PROGRESS.md`.


## Windows packaged build

GitHub Actions workflow `package-windows` creates a self-contained `win-x64` ZIP named `PrintAI-win-x64`.

Extract the complete ZIP and run `PrintAI.exe`. Do not copy the executable by itself because WebView2/PDFium/runtime assets are shipped beside it.

See `docs/INSTALL_WINDOWS.md` for package requirements and startup instructions.
