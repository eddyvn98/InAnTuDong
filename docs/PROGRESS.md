# Progress / Session Handoff

Last updated: 2026-09-28

## Current milestone

**M6 - Hardening & Release**

## Completed milestones

### M0 - Foundation
Complete.

### M1 - Deterministic A4 engine
Complete.

### M2 - Windows print path
Physically complete on Epson L3310, including exact-size PrintAI spooler verification.

### M3 - AI planner + desktop UX
Complete.

M3 now includes:

- WPF + WebView2 desktop shell
- JPG/JPEG/PNG/PDF source inspection
- PDFium rasterization
- multi-source / multi-page navigation
- multi-output physical A4 pagination
- provider-neutral AI planner
- strict PrintJobSpec 1.0 parsing/validation
- source-path allowlist
- Safe / Smart / Auto policy gate
- configurable chat-completions transport
- natural-language planning UI
- deterministic editable layout/print settings
- Windows printer selection/submission
- local capped job history
- self-contained Windows x64 package workflow

## Windows package

PR #10 adds `.github/workflows/package-windows.yml`.

It publishes:

```text
dotnet publish
  src/PrintAI.Desktop/PrintAI.Desktop.csproj
  Release
  win-x64
  self-contained
```

The package intentionally remains a folder-based publish instead of forcing a single executable because WebView2, PDFium and other native assets must remain beside the app.

Desktop executable:

```text
PrintAI.exe
```

Package also includes:

- `ui/`
- `INSTALL_WINDOWS.md`
- `BUILD.txt` with product/version/runtime/commit
- all .NET self-contained runtime files
- WebView2 managed/native support files
- PDFium/native dependencies

PR #10 package workflow run #1 successfully:

- published the app
- verified required package files
- measured 248.2 MB uncompressed output
- created the ZIP
- uploaded the `PrintAI-win-x64` GitHub Actions artifact
- final uploaded ZIP size: 98,099,691 bytes
- artifact SHA-256: `fd23fce532842ce6a21c4234afd249aed3dc54f7296decd7ffa99226a191f67a`

The normal CI run #57 is also green.

After merge, the same package workflow runs on `main` so the canonical main commit gets its own downloadable artifact.

## AI configuration

Optional planner configuration can be entered in the desktop UI or supplied through:

```text
PRINTAI_AI_ENDPOINT
PRINTAI_AI_MODEL
PRINTAI_AI_API_KEY
```

UI-entered API keys remain process-memory only and are not persisted in history.

See `docs/AI_PLANNER.md`.

## Physical verification

Epson L3310:

- calibration page measured correct by the user
- exact-size output submitted through the PrintAI spooler path measured correct by the user
- identity device profile remains ScaleX=1, ScaleY=1, OffsetX=0 mm, OffsetY=0 mm

## Current test baseline

Shared suite:

- 45 tests pass on Ubuntu and Windows

Windows-specific suite:

- 5 printer tests pass
- printer probe builds/smoke-runs
- WebView2 desktop build succeeds

Packaging:

- self-contained win-x64 publish succeeds
- required package layout verification succeeds
- ZIP artifact upload succeeds

## M4 - Scan + recipes

Software implementation complete and ready to merge.

Physical Epson L3310 scan validation is intentionally deferred and remains a post-merge hardware verification item.

### Implemented

Branch `m4-scan-recipes` now implements:

- replaceable `IScannerAdapter` boundary
- Windows WIA scanner enumeration and one-page capture
- 150/300/600 DPI desktop controls
- color / grayscale / black-and-white scan intent
- PNG or single-page A4 PDF scan output
- deterministic auto-crop and lightweight deskew processing
- reusable local recipe store
- apply/save/delete recipe desktop controls
- recipe source copies/layout/print/policy persistence
- direct-print recipe opt-in gated by verified printer profile and policy engine
- shared tests for crop, PDF scan output and recipe round-trip

The Windows desktop UI feeds a completed scan back into the existing source -> preview -> planner/policy -> spooler pipeline.

## M4 verification

PR #12 verification on commit `78fc1279b7d19cd14b8b27fa61a543cf374f6575`:

- CI run #65: Ubuntu + Windows success
- shared suite: 38/38 tests pass
- Windows printer tests/probe remain green
- WebView2 desktop build succeeds
- Windows package run #9: success
- `PrintAI-win-x64` artifact: 98,128,342 bytes
- artifact SHA-256: `9d28480867b795515e4242554d42332bae54358d9d6717f61d422c5df40bb064`

## Deferred hardware verification

When convenient on a Windows machine with the Epson L3310 driver installed:

1. Run a real WIA scan at 300 DPI.
2. Validate scan -> crop/deskew -> preview -> print physically.
3. Test PDF scan output and one saved recipe.
4. Tune WIA property handling/crop thresholds only if real hardware requires it.

This verification no longer blocks M5 development.

## M5 implementation in progress

Branch `m5-workflow-presets` adds the first deterministic preset layer:

- dedicated `PrintAI.Workflows` project
- CCCD 1:1 preset at 85.60 x 53.98 mm
- ID photo 3x4 preset
- ID photo 4x6 preset
- label/sticker 40x60 preset
- built-in workflow selector in the desktop app
- applied workflows become normal editable `PrintJobSpec` jobs
- workflow jobs can still be saved as reusable local recipes
- shared geometry/validation tests

See `docs/M5_WORKFLOWS.md`.

## M5 preset verification

PR #13 verification on commit `7fdf21b5aff58684f10f6dfd0247bf7651032e3d`:

- CI run #69: Ubuntu + Windows success
- shared suite: 45/45 tests pass
- Windows printer tests/probe remain green
- WebView2 desktop build succeeds
- Windows package run #13: success
- `PrintAI-win-x64` artifact: 98,137,222 bytes
- artifact SHA-256: `0cf6b70c5cf128aff7c86987166d22ad190b5d8cc57484ebe2e681c1625fe833`

## M5 mixed-job implementation in progress

Branch `m5-mixed-cccd` adds:

- `SourceSpec.PageIndex` for page-specific mixed sources
- deterministic source mapping on every layout placement
- multi-source A4 rendering
- mixed-source printing through the existing spooler path
- CCCD front/back composer using any two desktop source pages
- support for two pages from the same PDF
- synchronized copy edits across mixed sources
- shared tests for source ordering, page indexes, validation and mixed rendering

## M5 custom label verification

PR #15 is merged into `main`.

Baseline:

- main commit: `5217643c427fefb6fe960c7ceb52990ef36c2440`
- CI main run #75: success
- shared suite: 54/54 tests pass
- Windows package run #19: success

## M5 general composition + content-to-layout verification

PR #16 is merged into `main` at `a5f4b39b9c71ca5d822227dc1c3a49c0702bff97`.

Verification:

- CI run #76: Ubuntu + Windows success
- Windows package run #20: success

The merged slice adds the reusable mixed composition surface on top of the existing mixed-source renderer:

- select any number of approved source pages
- preserve explicit source/page order
- set a separate copy count for each selected source page
- choose common physical item width/height, gap, margin, fit, rotation and cut marks
- deterministic paper-saving grid layout across as many A4 output pages as required
- preserve per-source copy counts when later editing common layout settings
- preview remains required and printing continues through the existing spooler path
- cap total content items at 1000 per job
- shared tests for source order, per-source copies, pagination and invalid geometry

This is the first content-to-layout slice for schema 1.0: mixed content can vary by source/page and copy count while all placements in the composition share one physical item size. Irregular per-item physical sizes remain outside schema 1.0.

## M5 HEIC/HEIF verification

PR #17 is merged into `main` at `e72d304ac80f3eb9e4c16c655684d5751a2f635f`.

Verification:

- CI run #80: Ubuntu + Windows success
- Windows package run #24: success

The merged slice adds:

- `.heic` and `.heif` desktop file/folder input
- a replaceable `PrintAI.ImageDecoding` boundary
- PhotoSauce/libheif decode to PNG bytes
- HEIC dimensions through the existing source-inspection surface
- HEIC rendering through the existing Skia A4 renderer
- Windows/Linux native codec assets supplied by NuGet
- no changes to PrintJobSpec, physical layout, policy or spooler execution

## M5 Office conversion verification

PR #18 is merged into `main` at `f84b864caea6ee4119d5f56ab242535bcd1a7684`.

Verification:

- CI run #83: Ubuntu + Windows success
- Windows package run #27: success

The merged slice adds:

- DOC/DOCX/XLS/XLSX/PPT/PPTX input discovery
- `PrintAI.DocumentConversion` boundary
- LibreOffice headless conversion to isolated PDF work files
- converter discovery through `PRINTAI_LIBREOFFICE_PATH`, standard Windows paths and PATH
- per-conversion temporary LibreOffice user profile
- timeout/process-tree cleanup
- normal PDF inspection, preview, mixed-layout and spooler flow after conversion
- clear desktop error when LibreOffice is unavailable

LibreOffice remains optional and is not bundled with the Windows package.

## M5 printer profiles verification

PR #19 is merged into `main` at `53db0256f45f0f964b17e523fd16a6953b42f071`.

Verification:

- CI run #84: Ubuntu + Windows success
- Windows package run #28: success

The merged profile catalog contains:

- Epson L3310 — physically verified
- Epson L3316 — identity default, not physically verified
- Epson L3210 — identity default, not physically verified
- Epson L3250 — identity default, not physically verified
- generic A4 fallback — identity default, not physically verified

Unverified profiles do not claim exact-size calibration. The desktop gate now uses profile verification metadata rather than a hard-coded printer-name check.

## M5 software completion

The requested M5 software scope is implemented:

- workflow presets
- CCCD front/back
- customizable label sheets
- general mixed composition
- content-to-layout with uniform physical placement geometry
- HEIC/HEIF input
- optional Office-to-PDF conversion
- extensible printer profile catalog

Remaining validation items are intentionally non-blocking:

1. real Epson L3310 WIA scanner verification from M4
2. open/render a representative real iPhone HEIC/HEIF file in the packaged Windows app
3. verify DOCX/XLSX/PPTX conversion fidelity on a Windows machine with LibreOffice installed
4. physically calibrate L3316/L3210/L3250 only if those printers are actually used

Do not mark an unmeasured printer profile as physically verified.

## M6 release-readiness implementation in progress

Branch `m6-release-readiness` starts M6 with:

- app version `0.6.0`
- deterministic shared release-readiness evaluator
- desktop System Readiness panel
- core failures: unwritable work directory or no Windows printer
- optional warnings: unverified printer profile, no WIA scanner, no LibreOffice, no AI planner
- package `BUILD.txt` version sourced from the desktop project
- SHA-256 checksum generated for the Windows ZIP
- M6 scope and exit criteria documented in `docs/M6_RELEASE_READINESS.md`

## M6 smoke/regression verification

PR #22 is merged into `main` at `33fa87eb37aae6f249de97739582f4bc8ff0ad26`.

Verification:

- CI run #93: Ubuntu + Windows success
- Windows package run #37: success
- packaged `PrintAI.exe --self-test`: success
- release regression suite: success

The merged slice adds:

- `PrintAI.exe --self-test` headless startup mode
- JSON self-test report with application version/runtime/check results
- packaged UI asset verification
- packaged PNG inspection -> PrintJobSpec -> A4 rendering exercise
- packaged Windows printer-probe exercise without requiring a physical printer
- optional LibreOffice discovery reporting
- package workflow executes the published EXE before ZIP upload
- self-test JSON is uploaded beside the ZIP/checksum as release evidence
- shared regression tests for raster source, generated two-page PDF and mixed image+PDF rendering

## M6 install lifecycle verification

PR #23 is merged into `main` at `d75978064f3d5de4d08cdd52e1458c44390c9cc8`.

Verification:

- CI run #95: Ubuntu + Windows success
- Windows package run #39: success
- install -> installed self-test -> upgrade -> rollback -> uninstall: success

The merged slice adds:

- per-user `Install-PrintAI.ps1` with no admin requirement
- staged upgrade before replacing the current install
- automatic `.previous` retention for one-step rollback
- `Rollback-PrintAI.ps1` current/previous swap
- `Uninstall-PrintAI.ps1` that keeps local user data by default
- explicit `-RemoveData` opt-in for full local-data deletion
- Start Menu/Desktop shortcuts for normal installs
- portable ZIP usage remains supported
- Windows package CI exercises install -> installed self-test -> upgrade -> rollback -> uninstall in a sandbox

## M6 support report verification

PR #24 verification:

- CI run #97: Ubuntu + Windows success
- Windows package run #41: success
- packaged self-test + install lifecycle remain green

The slice adds a user-exportable JSON support report containing:

- Print AI version and runtime
- OS description
- loaded source/page counts only
- printer capability summary + resolved profile ID/physical-verification flag
- scanner names
- Office conversion availability
- AI planner configured/not-configured flag
- current System Readiness checks

Privacy boundary: the report intentionally excludes API keys, AI endpoint, source file paths, job history and document content.

## M6 4x6 auto-layout + Smart Collage verification

PR #26 is merged into `main` at `74639d982215c37a2cd9a060376bf84889355835` and adds a release-compatible workflow extension requested from real use:

- deterministic 4x6-inch paper preset at 101.6 x 152.4 mm
- 1-6 selected source/page inputs
- up to four portrait/landscape grid candidates
- Balanced / MinCrop / Fill preferences
- cached thumbnail previews for candidate selection
- selected candidate becomes the normal active PrintJobSpec
- Windows print submission now requests the job paper size instead of always forcing A4
- driver paper matching fails clearly when the requested stock is not advertised
- shared auto-layout geometry tests and Windows 4x6 paper-match test
- detailed behavior/acceptance criteria in `docs/AUTO_LAYOUT_4X6.md`

Smart Collage V2 foundation is merged:

- optional canvas scene graph without breaking existing grid/exact-size jobs
- independent per-frame geometry
- z-index and overlap
- per-frame fit, rotation, image scale and normalized offsets
- rectangle, rounded rectangle, ellipse and circle masks
- deterministic canvas validation
- initial library of 10 three-photo 4x6 portrait templates
- Smart Collage gallery action in the desktop UI
- scene-graph/template tests
- architecture and staged AI plan in `docs/SMART_COLLAGE_V2.md`

Smart Collage AI selection is merged:

- reuses the configured OpenAI-compatible chat endpoint/model/API key
- renders compact 768px source thumbnails for multimodal input
- sends all three images in one request
- asks the vision model for source importance/focal points, template choice, source-to-frame assignment and bounded zoom/pan
- strict parser rejects invented templates, duplicate/missing sources, invalid frame IDs and out-of-range transforms
- accepted proposals are converted to deterministic CanvasLayoutSpec jobs and rendered locally
- up to four AI alternatives are exposed in the existing preview gallery
- network/model/schema failures automatically fall back to deterministic collage templates
- pixel-level tests cover vision thumbnail sizing and actual circle-mask clipping

Current limitation: face/saliency detection is model-estimated rather than backed by a local CV detector. User preview remains mandatory.

Verification for PR #26:

- CI run #151: Ubuntu + Windows success
- shared Smart Collage/auto-layout tests: success
- Windows printer tests/probe: success
- Windows desktop build: success
- Windows package run #95: success
- self-contained publish: success
- packaged self-test: success
- install -> upgrade -> rollback -> uninstall lifecycle: success
- ZIP/checksum/artifact upload: success

Remaining field validation is intentionally separate from software verification:

- print a real 4x6 job on a driver/device that advertises 101.6 x 152.4 mm and measure the result
- run AI Smart Collage against a real configured vision-capable provider with representative photos
- face/saliency awareness is currently model-estimated; local CV hardening remains optional

Next Smart Collage work: manual canvas refinement and optional local face/saliency hardening.

Exact next M6 work:

1. real 4x6 paper/driver field validation
2. live multimodal Smart Collage field validation
3. manual Smart Collage refinement/editor
4. tagged preview release + release notes
5. real iPhone HEIC and scanner/Office/printer validation matrix

## Important execution boundary

AI cannot:

- calculate final printer/device coordinates
- bypass PrintJobSpec validation
- access unapproved local source paths
- bypass Safe/Smart/Auto policy
- directly call the Windows printer adapter

Execution remains:

source -> inspection -> planner/spec -> allowlist -> validation -> deterministic layout -> preview -> policy/user approval -> Windows print adapter.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and relevant specs first. Update this file before ending a work session.
