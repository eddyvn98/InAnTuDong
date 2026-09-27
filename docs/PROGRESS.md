# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M3 - Desktop UX first, AI planner second**

## Completed milestones

### M0 - Foundation

Complete.

### M1 - Deterministic A4 engine

Complete:

- millimetre-based domain/layout
- validation
- grid/repeat + rotation
- exact-size mode
- contain/cover
- cut marks
- SkiaSharp A4 rendering
- JPG/PNG/PDF inspection
- golden preview coverage

### M2 - Windows print path

Physically complete on Epson L3310:

- installed printer enumeration/capabilities
- A4 printable area/hard margins
- calibrated Epson L3310 identity profile
- Windows spooler submission
- spooler job status/errors
- calibration page
- physical calibration measured correct by the user
- exact-size output submitted through the PrintAI spooler path measured correct by the user

M2 no longer has a physical sizing gate.

## M3 desktop slice in PR #5

Branch: `feat/m3-desktop-shell`

### Windows desktop shell

New `PrintAI.Desktop` project:

- WPF native host
- Microsoft WebView2 stable package
- local HTML/CSS/JS UI loaded from the app output
- native/web message bridge

### Input

Implemented:

- multi-file picker
- folder picker
- window file/folder drop handler
- JPG/JPEG/PNG/PDF filtering
- duplicate prevention
- maximum 100 expanded inputs per session

### Inspection

Selected sources are passed through `PrintAI.SourceInspection`.

The desktop UI shows:

- file name
- source kind
- image pixel dimensions
- PDF page count
- per-source inspection errors

### Preview

For the first supported raster source:

1. create a deterministic default A4 `PrintJobSpec`
2. render a low-resolution A4 preview for the UI
3. separately render a 300 DPI A4 raster for physical printing

`RasterFilePreview` keeps file decoding at the rendering boundary.

A regression test covers real PNG file -> A4 raster preview.

### Printer + approval

The desktop app:

- enumerates installed Windows printers
- prefers Epson L3310 when present, otherwise the default printer
- lets the user explicitly select a printer
- keeps Print disabled until a raster preview exists
- requires the user to inspect the preview and press Print
- submits the 300 DPI raster through the already-calibrated Windows spooler path
- shows submission/job status text

This satisfies the first M3 preview-approval UX without involving AI.

## CI

The initial M3 desktop commit built successfully in PR #5 CI run #27:

- Ubuntu shared tests/build green
- Windows shared tests/build green
- Windows printer tests/build/smoke-run green
- Windows WebView2 desktop shell build green

The follow-up regression test/docs commits are running through the same matrix before merge.

## Remaining M3 work

1. PDF rasterization so PDF can preview/print through the same UI.
2. Multi-source/multi-page preview orchestration.
3. Provider-neutral AI planner interface.
4. Structured AI output -> `PrintJobSpec` parsing.
5. Safe / Smart / Auto policy engine.
6. User-editable print/layout controls around AI output.
7. Job history.
8. Package/publish the desktop app for normal Windows installation/use.

## Important design rule

Do not let AI talk directly to the printer or generate device coordinates.

The execution path remains:

source -> inspection -> planner/spec -> validation -> deterministic layout -> preview -> policy/user approval -> Windows print adapter.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and relevant specs first. Update this file before ending a work session.
