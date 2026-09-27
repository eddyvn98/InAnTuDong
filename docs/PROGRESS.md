# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M2 - Windows print path**

## Verified foundation

- M0 foundation is complete.
- M1 deterministic A4 engine is complete, including semantic golden preview coverage.
- GitHub Actions runs on Ubuntu and Windows.
- Railway project `InAnTuDong` / service `printai-web` deploys from `main`.
- Public web domain: `https://printai-web-production.up.railway.app`.

## M1 completed

- canonical millimetre geometry
- validation
- grid/repeat layout
- capacity-based 90-degree rotation
- exact-size mode
- contain/cover geometry
- cut marks
- SkiaSharp raster A4 preview
- JPG/PNG metadata inspection
- PDF page metadata inspection
- semantic golden preview test

## M2 capability probe implemented in PR #3

Branch: `feat/m2-printer-probe-calibration`

### Windows printer boundary

`PrintAI.Windows.Printing` now provides:

- installed-printer enumeration
- default-printer detection
- printer validity
- color capability
- duplex capability
- supported paper sizes converted to millimetres
- advertised printer resolutions
- A4 portrait printable area
- A4 hard margins
- printer-name matching, including an `L3310` model-token match

`PrintAI.WindowsProbe` outputs the capability snapshot as JSON.

Example:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- L3310
```

### Calibration

`PrintAI.Rendering` now generates an A4 calibration raster with:

- 10 mm inset outer frame
- 100 x 100 mm reference square
- 50 x 50 mm reference square
- 100 mm horizontal ruler with 1/5/10 mm ticks
- 100 mm vertical ruler with 1/5/10 mm ticks
- center cross

Web endpoints:

- `/api/calibration-a4.png?dpi=300`
- `/api/calibration-info`

The home page links to the 300 DPI calibration page.

## Verification

PR #3 CI run #16 is green on both operating systems.

Ubuntu:

- restore shared tests
- shared tests
- web build

Windows:

- restore shared tests
- shared tests
- web build
- restore Windows printer tests
- Windows printer tests
- Windows printer probe build
- Windows printer probe smoke-run

The smoke-run verifies the executable printer enumeration path starts and exits successfully on a real Windows GitHub runner.

## Physical verification required before spooler implementation

The software boundary is ready, but no CI environment has the user's Epson L3310.

On a Windows machine with the Epson driver installed:

1. run the printer probe with `L3310`
2. save the JSON output
3. open/print the calibration image at 100% / Actual Size
4. measure the 100 mm horizontal ruler
5. measure the 100 mm vertical ruler
6. measure the 100 x 100 mm square
7. record visible left/top edge offset or clipping

Do not implement production spooler submission until those physical measurements are known.

## Exact next engineering tasks

1. Capture the real Epson L3310 capability JSON.
2. Record physical calibration measurements.
3. Define a device profile from measured driver + physical behavior.
4. Implement Windows spooler submission using the validated profile.
5. Add job status/errors.
6. Verify an exact-size physical print with a ruler.

## Deferred / known gaps

- `DpiX` / `DpiY` are modeled but not normalized from every EXIF/JFIF/PNG combination.
- PDF page rasterization is not implemented.
- multi-source file loading/orchestration is not implemented.
- real spooler submission is intentionally gated on physical calibration.
- AI provider integration, WebView2 desktop shell, scanner control and recipes are not started.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and the relevant specs first. Update this file before ending a work session.
