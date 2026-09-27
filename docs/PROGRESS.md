# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M3 - Source pipeline complete; AI planner next**

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
- exact-size output submitted through PrintAI measured correct by the user

## M3 deterministic desktop/source pipeline

### Desktop shell

Implemented and merged previously:

- WPF + WebView2
- file/folder input
- drag/drop
- source inspection
- installed-printer selection
- explicit preview approval
- 300 DPI print rendering
- Windows spooler submission

### PDF rasterization

PR #6 adds `PDFtoImage 5.4.0` / PDFium behind `PrintAI.Rendering`.

`PdfPageRasterizer`:

- rasterizes one PDF page at a requested DPI
- returns an SKBitmap
- supports the Windows/Linux/macOS desktop runtime path
- keeps PDF-specific code out of Domain/Layout

`SourcePagePreview` now provides one shared entry point for:

- JPG/JPEG
- PNG
- PDF page N

The same A4 renderer is used after source decoding.

### Multi-source / multi-page orchestration

Desktop inputs are flattened into ordered `DesktopPage` items:

- raster image = one page
- PDF = one item per PDF page
- multiple files preserve input order

The desktop UI now:

- shows all source files
- shows a horizontal page strip
- allows selecting any source page
- previews the selected page
- prints the current page
- prints all flattened pages in order

For Print All, each page is rendered at 300 DPI immediately before spooler submission.

### Verification

PR #6 CI run #35 is green.

Ubuntu:

- shared restore/tests
- PDFium PDF page rasterization tests execute successfully
- source-page-to-A4 PDF preview test succeeds
- web build succeeds

Windows:

- shared tests including PDF rasterization
- web build
- Windows printer tests
- Windows printer probe build/smoke-run
- WebView2 desktop shell build

The first PDF implementation attempt was blocked by CA1416 platform analysis. The final implementation keeps an explicit runtime OS guard and a narrowly scoped analyzer suppression around the PDFtoImage call.

## Remaining M3 work

The deterministic source -> preview -> print path is now ready for AI integration.

Next:

1. provider-neutral AI planner interface
2. strict structured output -> `PrintJobSpec` parsing
3. planner validation/fallback behavior
4. Safe / Smart / Auto policy engine
5. user-editable print/layout controls around planner output
6. job history
7. package/publish desktop app for normal Windows installation

## Important design rule

AI may propose a `PrintJobSpec`, but it never:

- computes final device coordinates
- bypasses deterministic validation
- submits directly to the printer

Execution remains:

source -> inspection -> planner/spec -> validation -> deterministic layout -> preview -> policy/user approval -> Windows print adapter.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and relevant specs first. Update this file before ending a work session.
