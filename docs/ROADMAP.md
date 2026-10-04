# Roadmap

## M0 - Foundation

- [x] product specification
- [x] architecture
- [x] PrintJobSpec boundary
- [x] dependency policy
- [x] session handoff rules
- [x] persistent progress file
- [x] buildable domain/layout/web skeleton
- [x] GitHub Actions definition for Windows + Linux
- [x] CI observed green on GitHub
- [x] Railway deployment observed healthy

Exit: repo itself is enough to resume the project.

## M1 - Deterministic A4 engine

- [x] domain model
- [x] baseline validator
- [x] grid/repeat layout
- [x] choose 90-degree rotation when it fits more items
- [x] exact-size single item mode
- [x] fit/contain/cover rules
- [x] cut marks
- [x] SkiaSharp preview renderer
- [x] JPG/PNG metadata inspector
- [x] PDF metadata/page inspector
- [x] basic mm geometry tests
- [x] golden preview tests

Exit: JSON PrintJobSpec creates a trustworthy A4 preview.

## M2 - Windows print path

- [x] enumerate installed printers
- [x] select printer/profile
- [x] query capabilities/printable area
- [x] submit through Windows spooler/driver
- [x] status/errors
- [x] Epson L3310 calibration page
- [x] physical measurement verification
- [x] exact-size spooler physical verification

## M3 - AI planner + desktop UX

- [x] WPF/WebView2 shell
- [x] files/folder input
- [x] source inspection
- [x] PDF rasterization
- [x] multi-source/multi-page preview
- [x] provider-neutral AI planner
- [x] structured output parsing
- [x] Safe/Smart/Auto policy
- [x] preview approval
- [x] desktop natural-language planner integration
- [x] deterministic user-editable job settings
- [x] multi-output A4 preview/print
- [x] job history
- [x] desktop package/publish

## M4 - Scan + recipes

- [x] WIA adapter implementation
- [x] scan image/PDF pipeline
- [x] crop/deskew processing
- [x] reusable recipes
- [x] confidence/direct-print recipe rules
- [~] physical Epson L3310 scan validation — deferred; track as post-merge hardware verification

## M5 - Advanced workflows

**Current milestone after M4 software merge.**

- [x] CCCD — 1:1 single-side + front/back composition
- [x] ID photo presets — 3x4 and 4x6
- [x] labels/stickers — 40x60 preset + customizable A4 label sheets
- [x] content-to-layout — general mixed-source uniform-size composition
- [x] mixed jobs — reusable multi-source/page composition UI
- [x] HEIC — HEIC/HEIF decode through replaceable adapter
- [x] Office conversion — optional LibreOffice adapter to PDF; real-file fidelity check deferred
- [x] additional printer profiles — verified/unverified catalog with generic fallback

## M5 deferred real-world verification

- [~] real Epson L3310 WIA scan validation
- [~] representative HEIC/HEIF file in packaged Windows app
- [~] DOCX/XLSX/PPTX fidelity with installed LibreOffice
- [~] physical calibration for L3316/L3210/L3250 only when those devices are available

These are validation tasks, not missing software architecture.

## M6 - Hardening & Release

**Current milestone after M5 software completion.**

- [~] in-app system readiness diagnostics
- [~] versioned Windows package manifest + SHA-256 checksum
- [x] representative core regression fixtures — raster/PDF/mixed pipeline; real HEIC remains field validation
- [x] packaged Windows smoke test — published EXE self-test verified in package CI
- [x] install / upgrade / rollback flow — lifecycle scripts verified by Windows package CI
- [x] support diagnostics / error export — privacy-safe support report verified
- [x] deterministic 4x6 auto-layout alternatives — 1-6 items, preview gallery, driver-aware paper submission; real 4x6 physical print remains field validation
- [x] Smart Collage V2 — canvas placements, masks, z-order, transforms and initial three-photo template library
- [~] Smart Collage AI — multimodal analysis, template selection, source assignment, bounded crop/scale/offset and top-candidate ranking implemented; live provider field validation and optional local face/saliency hardening remain
- [ ] Smart Collage manual refinement — select frame, pan/zoom image, move/resize/rotate, undo/redo
- [x] Excel Smart Print software integration — PR #28 merged; CI + package green
- [x] manual duplex correctness — PR #29 merged; planner, batch spooler, auto/manual execution, reinsert state and restart-safe back pass verified by CI + Windows package
- [~] manual duplex printer calibration — per-printer order/rotation/instruction persistence implemented; real-paper verification pending
- [ ] tagged release + release notes
- [ ] field validation matrix for real 4x6, Smart Collage provider, HEIC, Office, scanner and printer hardware — execute `docs/M6_FIELD_VALIDATION.md`

Exit: a versioned Windows package can be verified, diagnosed, installed and exercised on a real target machine with release-blocking failures visible before printing.

See `docs/M6_RELEASE_READINESS.md`.


## M7 - General Print Intent

**Direction correction: broaden print-language coverage while keeping the product print-only.**

The existing M0-M6 execution work remains valid. M7 adds a high-level `PrintPlan 2.0` above `PrintJobSpec 1.0` so real requests can use multiple page/file/rule groups without rewriting the stable layout/spooler path.

### Foundation slice

- [x] document print-only scope and exclusions
- [x] add `PrintPlan 2.0` domain model
- [x] page ranges with include/exclude and odd/even parity
- [x] ordered output groups
- [x] explicit sets vs collate semantics
- [x] mixed color/duplex decomposition
- [x] deterministic compiler to one or more `PrintJobSpec 1.0` batches
- [x] strict general-planner JSON parser
- [x] source path + inspected page-count binding
- [x] unit tests added for the foundation slice
- [x] CI verification for the foundation slice — CI #174 and package-windows #118 succeeded

### Product integration slice

- [x] desktop integration for complex natural-language requests — GeneralPrintPlanner is the main AI request path
- [x] multi-batch preview UX — explicit batch strip + selected batch preview
- [x] multi-batch execution coordinator — full-plan auto sequence for simplex; duplex remains safe batch-by-batch
- [x] cross-group complete-set ordering
- [x] Windows driver execution honors color/grayscale intent
- [x] integration verification — CI #191 + package-windows #135 succeeded

### Coverage-validation slice

- [x] 150-case real-request print-intent corpus across 15 balanced categories
- [x] explicit supported / capability-dependent / clarification / gap labels
- [x] CI validation for corpus size, category balance, IDs and gap labels
- [x] request-derived deterministic compiler regression fixtures
- [x] corpus slice CI verification — CI #199 and package-windows #143 succeeded

Baseline after corpus creation: 76 supported, 10 capability-dependent, 10 clarification, 54 deterministic gaps. See `docs/PRINT_INTENT_CORPUS.md`.

### General N-up slice

- [x] explicit `NUpSpec.pagesPerSheet` semantic contract
- [x] deterministic 2 / 4 / 6 / 8 / 9 / 16-up grids
- [x] auto paper orientation + explicit orientation override
- [x] optional explicit column count for presentation-style N-up
- [x] row-major page ordering
- [x] N-up gap / margin
- [x] item border rendering
- [x] N-up + duplex composition
- [x] all 10 N-up corpus cases moved from gap to supported
- [x] General N-up CI/package verification — CI #204 and package-windows #148 succeeded

Updated corpus: 86 supported, 10 capability-dependent, 10 clarification, 44 deterministic gaps.

### Physical scaling slice

- [x] trusted per-page PDF physical dimensions from SourceInspector
- [x] planner binder rejects rewritten physical page sizes
- [x] `PhysicalScaleMode.MaxFit`
- [x] `PhysicalScaleMode.ShrinkOnly`
- [x] `PhysicalScaleMode.Percent`
- [x] explicit percent / ratio mapping
- [x] per-page physical sizes survive compilation
- [x] renderer applies scaling in physical mm
- [x] desktop batch UI exposes scaling mode
- [x] all 6 scaling gap cases moved to supported
- [x] physical scaling CI/package verification — final PR head CI #213 and package-windows #157 succeeded

Updated corpus: 92 supported, 10 capability-dependent, 10 clarification, 38 deterministic gaps.

### Crop / placement slice

- [x] asymmetric page margins
- [x] exact-size placement shrink-to-fit within asymmetric margins
- [x] center / edge / corner anchors
- [x] signed millimetre X/Y offsets
- [x] deterministic auto-trim-white crop
- [x] center-to-target-aspect crop
- [x] physical edge crop in millimetres
- [x] trusted physical-size validation for millimetre crop
- [x] desktop batch UI exposes placement/crop
- [x] all 8 crop/position gap cases moved to supported
- [x] crop/placement CI/package verification — CI #219 and package-windows #163 succeeded

Updated corpus: 100 supported, 10 capability-dependent, 10 clarification, 30 deterministic gaps.

### Booklet imposition slice

- [x] high-level `BookletSpec`
- [x] standard A4 landscape -> folded A5 geometry
- [x] deterministic multiple-of-four blank padding
- [x] deterministic physical side reordering
- [x] virtual blank source rendering
- [x] short-edge duplex execution
- [x] configurable center gutter
- [x] complete booklet sets via collated batches
- [x] all 10 booklet gap cases moved to supported
- [x] booklet CI/package verification — final PR-head CI #225 and package-windows #169 succeeded

Updated corpus: 110 supported, 10 capability-dependent, 10 clarification, 20 deterministic gaps.

### Poster / tiled printing slice

- [x] high-level `PosterSpec`
- [x] target width/height in physical millimetres
- [x] missing dimension from trusted source aspect
- [x] fixed rows/columns grid
- [x] automatic minimum-sheet orientation
- [x] physical overlap
- [x] partial final tile with exact physical dimensions
- [x] poster-level Contain/Cover source mapping
- [x] registration marks on shared edges
- [x] tile row/column + sequential labels
- [x] complete poster sets via collated batches
- [x] all 10 poster gap cases moved to supported
- [x] poster/tile CI/package verification — CI #233 and package-windows #177 succeeded

Updated corpus: 120 supported, 10 capability-dependent, 10 clarification, 10 deterministic gaps.

### Variable-size item slice

- [x] high-level `VariableItemsSpec`
- [x] independent source/page per item
- [x] independent physical width/height
- [x] independent copies and fit
- [x] one-dimension derivation from trusted source aspect
- [x] preserve trusted physical source size when dimensions are omitted
- [x] deterministic mixed-size paper-saving packer
- [x] per-item 90-degree rotation permission
- [x] multi-page Canvas execution
- [x] isolated rotated-footprint flag preserving Smart Collage semantics
- [x] optional cut marks
- [x] 1000-placement safety cap
- [x] desktop batch UI exposes mixed-size intent
- [x] all 10 variable-size gap cases moved to supported
- [x] variable-size CI/package verification — CI #239 and package-windows #183 succeeded

Updated corpus: 130 supported, 10 capability-dependent, 10 clarification, 0 deterministic primitive gaps.

Exit: representative real print requests can be expressed as a validated PrintPlan, compiled deterministically into executable jobs, previewed and printed without adding business-management scope.

See `docs/GENERAL_PRINT_INTENT.md`.

## Priority

M7 deterministic primitive coverage is complete. Prioritize M8 Antigravity target-machine verification, latency measurement, local-host UX and release hardening. Do not expand into non-print business workflows.


## M8 - Antigravity local planner

**Current milestone after M7 deterministic coverage.**

- [x] replace primary API-key planner transport with local `agy.exe`
- [x] auto-discover AGY from explicit path / `PRINTAI_AGY_PATH` / PATH
- [x] fast-first structured planning with low effort
- [x] deterministic parser/validator/confidence gate
- [x] one-time escalation to configurable deep AGY model
- [x] transport-failure escalation
- [x] remove endpoint/API-key fields from desktop planner UI
- [x] preserve deterministic Smart Collage fallback while AGY image handling is unverified
- [x] document AGY credentials boundary and low-spec target assumptions
- [x] unit tests for fast accept / low-confidence escalation / transport escalation
- [x] verify AGY headless availability on the target Windows machine
- [ ] benchmark cold single-process latency
- [ ] add persistent `stream-json` session if measurements justify it
- [ ] migrate Smart Collage local-image analysis to AGY without reintroducing API keys
- [~] loopback-only local host — core upload → AGY → preview → print browser flow implemented; advanced desktop controls still migrating
- [~] general artifact agent — print/artifact/artifactThenPrint routing, isolated task workspace, Office-original retention and output re-import implemented in PR #47; target-machine AGY tool execution verification pending
- [x] macOS loopback web workflow — source upload, deterministic A4 preview, 300 DPI PDF export, and selected CUPS queue submission
- [x] optional macOS AGY natural-language planner — strict schema, uploaded-source binding, deterministic layout, mandatory preview
- [x] local-web multi-file gallery — automatic upload, source thumbnails, multi-selection, removal and sequential AGY request queue
- [~] macOS physical print validation — adapter builds and enumerates CUPS; this Mac has no configured printer, so scale/driver fidelity needs a real device

Exit: natural-language planning works on the target Windows machine through the user's existing Antigravity account, without an AI API key or local LLM, with measured latency and deterministic print safety unchanged.
