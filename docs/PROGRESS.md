# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M2 - Windows print path**

## Verified foundation

- M0 foundation is complete.
- M1 deterministic A4 engine is complete.
- Railway web demo is healthy.
- Epson L3310 calibration references were physically measured by the user and reported correct.
- Calibrated L3310 profile therefore uses identity correction:
  - ScaleX = 1.000
  - ScaleY = 1.000
  - OffsetX = 0 mm
  - OffsetY = 0 mm

## M2 implemented

### Capability probe

`PrintAI.Windows.Printing` provides:

- installed-printer enumeration
- printer selection/matching
- default/valid/color/duplex capability
- paper sizes and resolutions
- A4 printable area
- hard margins

### Calibration

The A4 calibration renderer provides known 100 mm and 50 mm geometry.

The user physically measured the calibration output on Epson L3310 and reported all reference dimensions/placement correct.

### Windows spooler submission

PR #4 adds:

- `PrinterDeviceProfile`
- calibrated Epson L3310 identity profile
- `WindowsSpoolerPrinter.SubmitA4Png`
- A4 portrait selection through the installed Windows driver
- hard-margin origin compensation
- full A4 image drawn at 210 x 297 mm
- silent `StandardPrintController` submission
- copies support
- structured submission failures
- unique PrintAI document names

CLI:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- print L3310 "C:\path\page.png"
```

### Job status

`SpoolerJobMonitor` maps Windows spooler state into:

- Queued
- Printing
- Completed
- Error
- Deleted
- Unknown

CLI:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- status "EPSON L3310 Series" <job-id>
```

The implementation uses `System.Printing` to inspect the local Windows queue. Very fast completed jobs may disappear before lookup; that case is reported as not-found-or-already-completed.

## CI verification

Latest implementation CI passed the meaningful Windows pipeline steps:

- shared restore/tests
- web build
- Windows printer restore
- Windows printer tests
- Windows printer probe build
- Windows printer probe smoke-run

CI never performs a physical print.

## Final M2 hardware gate

One final physical verification remains:

1. generate/use an A4 PNG from PrintAI
2. submit it through the new `print L3310 ...` command
3. measure a known exact-size element
4. confirm the PrintAI spooler path itself preserves the already-calibrated physical scale

After that passes, M2 can be considered physically closed and work should move to M3 desktop UX + planner.

## Deferred / known gaps

- `DpiX` / `DpiY` normalization is incomplete.
- PDF page rasterization is not implemented.
- multi-source file loading/orchestration is not implemented.
- AI provider integration, WebView2 desktop shell, scanner control and recipes are not started.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and the relevant specs first. Update this file before ending a work session.
