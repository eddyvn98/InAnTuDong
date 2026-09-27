# Windows printing path

## Calibration status

The Epson L3310 physical calibration page was measured on 2026-09-27.

Result reported by the user: reference rulers, squares and placement were all physically correct.

The initial Epson L3310 device profile therefore uses identity correction:

- ScaleX: 1.000
- ScaleY: 1.000
- OffsetX: 0 mm
- OffsetY: 0 mm

This calibration applies to the printer/driver/settings combination that was physically tested. Recalibrate if the driver, borderless mode, paper/media configuration or relevant scaling settings change.

## Submit an A4 raster

The print command is intentionally explicit. CI never invokes it.

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- print L3310 "C:\path\page.png"
```

Optional copies:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- print L3310 "C:\path\page.png" 2
```

The command:

1. finds the installed printer matching `L3310`
2. selects A4 portrait from the installed driver
3. uses the calibrated L3310 identity profile
4. compensates the driver's hard-margin graphics origin
5. draws the full A4 raster at 210 x 297 mm
6. submits through the installed Windows printer driver
7. returns a document name and, when still visible in the queue, a spooler job ID

## Query job status

When a job ID is returned:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- status "EPSON L3310 Series" 123
```

Possible normalized states:

- Queued
- Printing
- Completed
- Error
- Deleted
- Unknown

A very fast completed job can disappear from the queue before it is queried. In that case the CLI reports `not-found-or-already-completed`.

## Final M2 verification

The remaining physical test is to submit an exact-size A4 raster through this PrintAI spooler path and measure the printed result. This verifies that PrintDocument + the Epson driver preserves the same physical scale as the already-passed calibration print path.
