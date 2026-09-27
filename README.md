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
  -> AI planner
  -> PrintJobSpec
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

Then open the address printed by ASP.NET Core.

Windows desktop shell:

```powershell
dotnet run --project src/PrintAI.Desktop/PrintAI.Desktop.csproj
```

The desktop app currently supports JPG/PNG preview + printing and JPG/PNG/PDF source inspection. PDF raster preview/printing is still a later slice.

See `docs/PROGRESS.md` for the exact current state.

## Optional AI planner gateway

The desktop app can use a provider-neutral PrintAI planner gateway.

Set:

```powershell
$env:PRINTAI_PLANNER_URL = "https://your-planner.example/api/plan"
$env:PRINTAI_PLANNER_TOKEN = "optional-bearer-token"
```

The gateway receives:

```json
{
  "systemInstruction": "...",
  "userPayload": "..."
}
```

and returns the raw planner JSON envelope required by `PrintJobPlanParser`.

No model-provider API key is stored in the desktop source code.
