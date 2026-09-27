# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M5 - Advanced workflows**

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

## M5 general composition + content-to-layout in progress

Branch `m5-general-composition` adds the reusable mixed composition surface on top of the existing mixed-source renderer:

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

## Exact next work - M5

1. Verify general composition/content-to-layout in Linux + Windows CI and package build.
2. Add HEIC support behind source inspection/rendering boundaries.
3. Add Office conversion behind a conversion adapter.
4. Add additional printer profiles after format conversion is stable.

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
