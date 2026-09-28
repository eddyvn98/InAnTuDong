# Architecture Decision Log

## ADR-001 - Windows-first hybrid desktop
Status: accepted

Use a Windows native agent/host with web-based UI capability. Pure browser code cannot reliably own the required local file/printer/scanner integration.

## ADR-002 - Bypass manufacturer UI
Status: accepted

Use Windows printing APIs and installed drivers for normal jobs. Epson UI automation is fallback only.

## ADR-003 - PrintJobSpec is the execution boundary
Status: accepted

AI produces structured intent. Validated deterministic code owns execution.

## ADR-004 - Millimetres are canonical
Status: accepted

Physical geometry is stored/calculated in millimetres and converted only at rendering/device boundaries.

## ADR-005 - Library-first commodity implementation
Status: accepted

Use mature libraries/platform APIs where they remove meaningful commodity code. Keep custom code focused on intent, validation, layout policy and orchestration.

## ADR-006 - .NET 10 baseline
Status: accepted

Use .NET 10 for the new codebase.

## ADR-007 - Railway hosts the cross-platform web surface only
Status: accepted

Railway verifies/demoes shared domain/layout/web behavior. Windows printer/scanner execution remains on a local Windows agent.

## ADR-008 - Exact-size does not imply scaling
Status: accepted

`LayoutMode.ExactSize` preserves the requested physical width/height in millimetres. The first implementation places one exact-size item per page, centers it in the printable layout area, and may rotate 90 degrees only when rotation is required to fit and is explicitly allowed.

## ADR-009 - Fit geometry is separate from physical placement
Status: accepted

`Contain` and `Cover` control how source pixels map into an already-determined physical placement. They never change the placement's millimetre dimensions.

## ADR-010 - Source inspection is separate from layout
Status: accepted

JPG/PNG/PDF inspection lives behind a source-inspection boundary. SkiaSharp handles raster decode/orientation, MetadataExtractor reads available image metadata, and PDFsharp reads PDF page metadata. Layout remains file-format independent.

## ADR-011 - Windows driver capability data is advisory until calibrated
Status: accepted

Printer paper sizes, printable area, hard margins, color and duplex capability are read from the installed Windows driver. These values define the initial device capability boundary, but exact physical scale is not trusted until a calibration page is printed and measured.

## ADR-012 - Windows printer code stays out of the Railway path
Status: accepted

Printer enumeration and capability inspection live in a Windows-only project. Shared domain/layout/rendering remain cross-platform and the Railway web demo does not reference the Windows printing assembly.

## ADR-013 - AI planner returns a versioned structured proposal
Status: accepted

The AI boundary returns a strict JSON envelope containing a versioned `PrintJobSpec`, confidence, questions and warnings. Unknown JSON fields, unsupported enum values, unsupported schema versions and domain-invalid jobs are rejected before layout.

## ADR-014 - Policy engine is independent from the model provider
Status: accepted

Safe/Smart/Auto execution decisions are deterministic code. The model may report confidence/questions/warnings, but cannot bypass validation, preview policy or the Windows print adapter.

## ADR-015 - Planner source paths are allowlisted
Status: accepted

AI planner output may reference only source paths that the desktop app already selected and inspected. A proposal that invents or rewrites a source path is rejected before preview/rendering.

## ADR-016 - Desktop AI credentials are session-only by default
Status: accepted

The desktop UI may accept an API key for the active process, but it does not persist that key to job history or app settings. Environment variables are also supported for managed/local configuration.

## ADR-017 - One source page may create multiple A4 output pages
Status: accepted

Planner/layout output pagination is distinct from source pagination. The desktop UI exposes both source-page navigation and A4 output-page navigation, and a planned job can submit all output pages in deterministic order.


## ADR-018 - Built-in workflows compile to PrintJobSpec
Status: accepted

M5 built-in workflows are deterministic preset definitions that create ordinary validated `PrintJobSpec` jobs. They may prefill physical size, fit, copy count, quality and cut marks, but they do not bypass validation, preview/policy gates or the Windows print adapter. User edits and reusable recipes remain downstream of the same job boundary.


## ADR-019 - Mixed jobs map every placement to a source
Status: accepted

A layout placement carries `SourceIndex` and `SourceCopyIndex`. `SourceSpec` also carries a zero-based `PageIndex` so different pages from the same PDF can participate in one job.

The deterministic layout engine decides geometry only. The renderer resolves each placement back to its approved source and page. This keeps multi-source composition reusable for CCCD front/back, label sheets and future content-to-layout workflows without introducing workflow-specific printer code.


## ADR-020 - Content-to-layout starts with uniform placement geometry
Status: accepted

For PrintJobSpec schema 1.0, a mixed composition may contain many source pages with independent copy counts, but every placement in that job shares the same physical item width/height, gap, margin, fit and rotation policy.

The existing mixed-source renderer remains responsible for resolving each placement to its explicit source/page. The deterministic grid engine remains responsible for A4 packing and pagination.

Per-source copy counts are preserved when common layout settings are edited. Irregular per-item physical dimensions are deferred until a future schema revision demonstrates a concrete need; schema 1.0 is not expanded prematurely.


## ADR-021 - HEIC decode is an adapter before inspection/rendering
Status: accepted

HEIC/HEIF support lives behind `PrintAI.ImageDecoding`. The first adapter uses PhotoSauce MagicScaler with libheif to decode the source into PNG bytes.

Source inspection and rendering consume the decoded raster through their existing Skia paths. Domain, Layout, PrintJobSpec, policy and printer execution remain file-format independent.

The app does not depend on the Windows HEIF/HEVC Store codec because availability differs by machine. The decoder adapter can be replaced later without changing physical-layout or printer code.


## ADR-022 - Office files are converted before entering PrintJobSpec
Status: accepted

Word, Excel and PowerPoint documents are not rendered directly by Domain/Layout. A replaceable document-conversion boundary first converts supported Office files to PDF, after which the existing PDF inspection/rendering pipeline is reused.

The first adapter calls LibreOffice headlessly with its documented `--convert-to pdf --outdir` interface. LibreOffice is optional and external to the PrintAI package; the app discovers it from explicit configuration, standard installation paths or PATH and reports a clear error when unavailable.

Office COM automation is not used because it would require Microsoft Office installation and interactive desktop assumptions.


## ADR-023 - Printer profile verification is explicit metadata
Status: accepted

Printer selection resolves through a catalog of device profiles. A profile may contain default scale/offset values without implying those values were physically measured.

Only a profile that has completed real printed measurement may set `IsPhysicallyVerified=true`. The Epson L3310 profile is currently the only verified profile.

L3316, L3210, L3250 and the generic A4 fallback use identity transforms as safe defaults but remain unverified. Unverified profiles must not unlock verification-dependent direct-print behavior. The desktop recipe/direct-print gate reads the catalog's verification flag instead of inferring verification from the printer name.


## ADR-024 - Auto layout is deterministic and paper-aware
Status: accepted

Auto-layout alternatives are generated by deterministic local code, not by repeated LLM calls. The first implementation enumerates uniform grid candidates compatible with PrintJobSpec schema 1.0, tries portrait and landscape paper orientation, scores the candidates and exposes a small preview set for user selection.

4x6 photo paper is represented as 101.6 x 152.4 mm. Rendering and Windows submission both follow the job's PaperSpec. The Windows adapter must match a paper size advertised by the installed driver within a small rounding tolerance; it must not silently scale a requested 4x6 job onto A4.

Irregular collage geometry is deferred until an explicit custom-placement schema is introduced.


## ADR-025 - Smart Collage AI proposes bounded transforms, deterministic code owns geometry
Status: accepted

Smart Collage reuses the application's configured OpenAI-compatible model endpoint for multimodal analysis when the selected model supports image input. All selected source images are downscaled and sent together so the model can compare their relative visual importance.

The model may choose only known collage template IDs, assign the three approved sources to template frames, and propose bounded image scale plus normalized X/Y crop offsets. It may not invent physical frame coordinates or printer geometry.

A strict parser rejects unknown templates, missing/duplicate sources, invalid frame indices and out-of-range transforms. Accepted proposals are converted into normal CanvasLayoutSpec jobs, validated and rendered locally. If the model is unavailable, malformed or does not support vision, PrintAI falls back to deterministic templates instead of blocking printing.

No local face detector is required by this initial implementation; subject/face awareness from the vision model is advisory and preview remains required.


## ADR-026 - Duplex intent resolves to automatic or guided manual execution
Status: accepted

`PrintJobSpec.Print.Duplex` remains printer-neutral intent. `LongEdge` and `ShortEdge` must never be silently treated as `Off`.

After printer selection, deterministic execution chooses the physical strategy. A printer advertising automatic duplex uses one multi-page driver/spooler job with an orientation-aware duplex setting. A simplex printer uses a guided two-pass manual-duplex workflow.

Manual duplex is modeled by physical sheets. PrintAI submits the complete front pass, persists the approved back-pass plan, blocks unrelated printing while the paper stack is pending, requires an explicit reinsert action, then submits the complete back pass. Restart or a failed back pass must never cause the front sides to be printed again.

Back-pass order, rotation and reinsert instructions are explicit per-printer profile data. Verification is persisted only after a real-paper confirmation and is never inferred from the printer model name.

See `docs/MANUAL_DUPLEX.md`.


## ADR-027 - PrintPlan 2.0 expands intent without replacing PrintJobSpec 1.0
Status: accepted

PrintAI remains a print-only product. CRM, pricing, payment, inventory, delivery and unrelated order-management scope are explicitly excluded.

`PrintJobSpec 1.0` remains the stable executable print contract. It is appropriate for uniform jobs where all selected content shares one paper/layout/print rule set.

`PrintPlan 2.0` is added above that boundary for real-world requests that require page ranges, multiple approved sources, ordered output groups, different color/duplex/layout rules, or explicit sets/collation.

The AI may propose a strict `PrintPlan 2.0`, but deterministic code validates source/page references and compiles it into one or more ordinary `PrintJobSpec 1.0` batches. Every compiled job is validated again before layout/preview/execution.

This avoids destabilizing the existing renderer/spooler architecture while allowing print-language coverage to grow without creating a preset for every named print product.

See `docs/GENERAL_PRINT_INTENT.md`.


## ADR-028 - Multi-batch execution is automatic only when paper handling is safe
Status: accepted

A compiled PrintPlan may contain multiple executable PrintJobSpec batches.

The desktop UI exposes every batch explicitly and allows preview/printing one batch at a time. When all batches are simplex, PrintAI may submit the complete plan sequentially in deterministic batch order.

If any batch requests duplex, PrintAI does not auto-advance the whole multi-batch plan. The user executes batches explicitly so an automatic/manual duplex pass, paper reinsert step or pending back pass cannot accidentally flow into the next logical batch.

Complete collated sets spanning multiple output groups are interleaved by set: all groups for set 1, then all groups for set 2, and so on.

Color mode is part of executable intent, not UI metadata. Windows submission must honor PrintJobSpec.Print.ColorMode, and a persisted manual-duplex back pass must preserve the same color mode.


## ADR-029 - General N-up compiles to the existing Grid execution mode
Status: accepted

General document pages-per-sheet intent is represented at the PrintPlan 2.0 layer as `NUpSpec`; it does not add another executable `LayoutMode`.

The supported first-slice values are 2, 4, 6, 8, 9 and 16 pages per sheet. Deterministic code chooses the standard row/column grid, paper orientation and physical cell dimensions. The planner may request an explicit valid column count when the request clearly implies a different grid, such as presentation slides.

`NUpSpec` owns pages-per-sheet, optional columns, gap, margin, border, fit and auto-orientation. The compiler resolves those semantics into a normal `LayoutMode.Grid` PrintJobSpec with uniform cell geometry.

Source pages remain in explicit row-major order. Duplex remains a separate print setting and is not reinterpreted by N-up. An incomplete final sheet is allowed and contains the remaining source pages without duplication.

Borders are an executable layout property and are rendered locally; the model does not draw or rasterize borders itself.

This preserves the stable Grid renderer, preview pipeline, Windows spooler and manual/automatic duplex implementation while closing the General N-up corpus gap.


## ADR-030 - Physical scaling is separate from fit/crop geometry
Status: accepted

Physical print scaling and source-pixel fit are different concerns.

`FitMode.Contain` / `Cover` continue to describe how source pixels map into an already-defined placement. General Print Intent adds an optional `PhysicalScaleSpec` for requests expressed relative to the source's real printed size.

The supported physical scale modes are:

- `MaxFit`: use the largest whole-content size that fits the target placement.
- `ShrinkOnly`: preserve inspected source physical size unless shrinking is required; never upscale.
- `Percent`: multiply inspected source physical width/height by an explicit percentage.

For PDF, SourceInspector supplies per-page width/height in millimetres. These dimensions cross the AI boundary as source context but are rebound from deterministic inspected metadata before compilation. Planner output cannot rewrite them.

`ShrinkOnly` and `Percent` require trusted physical source dimensions. If those dimensions are unavailable, compilation is blocked; the planner should ask rather than infer a physical size from pixels.

Percent values are centered in the target placement. Percentages larger than the target may be clipped by that placement, and preview exposes the result. This is distinct from `Cover`, which intentionally crops to fill.

Physical scaling is not combined with General N-up or Canvas in this slice. This avoids mixing page-scale semantics with multi-item/collage geometry before a concrete need is validated.


## ADR-031 - Page placement and source crop are separate deterministic primitives
Status: accepted

Page-space positioning and source-space cropping are different print operations and remain separate in the execution contract.

`PagePlacementSpec` controls the physical placement rectangle on paper:

- asymmetric margins
- page anchor
- signed X/Y offsets in millimetres
- optional proportional shrink-to-fit when the requested margins reduce available space

It currently applies only to `LayoutMode.ExactSize`. Offsets may move content outside the requested margin box but may not move the physical placement outside the paper.

`SourceCropSpec` controls which part of the source is mapped into that placement:

- `AutoTrimWhite` detects a deterministic non-white bounding box
- `CenterToTargetAspect` crops symmetrically to the placement aspect
- `EdgesMm` removes explicit physical millimetres from source edges

`EdgesMm` requires trusted source physical dimensions. PDF dimensions come from SourceInspector and planner output cannot override them.

Crop is applied before Contain/Cover pixel mapping. General crop is not combined with N-up, Canvas, or physical scaling in this slice; placement and crop may be combined.

This separation prevents margin/position requests from being misrepresented as source crop, and prevents source crop from silently changing paper-space positioning.


## ADR-032 - Booklet imposition reorders logical pages before the existing Grid and duplex paths
Status: accepted

Booklet is represented at the PrintPlan layer by `BookletSpec`. It does not introduce a new executable layout mode or printer adapter.

The model keeps selected pages in normal reading order. Deterministic code pads the logical page sequence to a multiple of four, then emits booklet side order per physical sheet.

For eight logical pages:

- front sheet 1: 8, 1
- back sheet 1: 2, 7
- front sheet 2: 6, 3
- back sheet 2: 4, 5

Missing logical pages are represented as `SourceSpec.IsBlank=true` with no source file path. The renderer creates a white raster locally for those placements. Blank padding never invents a path or page in an approved source.

The imposed sequence is lowered to the existing two-column `LayoutMode.Grid` on landscape paper. The center gutter is the grid gap; the booklet outer margin is the grid margin.

Booklet execution uses `DuplexMode.ShortEdge`. Automatic duplex remains a driver decision, and simplex printers reuse the existing guided manual-duplex planner/profile. The booklet layer does not implement a second duplex engine.

Booklet sets require collated complete-set semantics. General N-up, physical scaling, general crop, page placement, and Canvas are not combined with booklet in this slice.

This keeps page-order logic deterministic while preserving the existing preview, raster, Windows spooler, and manual-duplex execution boundaries.


## ADR-033 - Poster tiling uses a poster-space canvas and per-sheet PosterTile execution mode
Status: accepted

Poster printing cannot be represented faithfully as ordinary uniform Grid or ExactSize placement because the final row/column may cover less physical poster area than interior tiles. Stretching those partial tiles to a full A4 cell would change the assembled poster scale.

`PosterSpec` therefore remains high-level PrintPlan intent, while compilation emits a `LayoutMode.PosterTile` PrintJobSpec.

The poster resolver owns:

- assembled target width/height
- fixed or automatically calculated rows/columns
- overlap in millimetres
- automatic portrait/landscape choice
- exact poster-canvas rectangle covered by every physical sheet

Every compiled source carries `PosterTileSourceSpec` metadata identifying its row/column and its rectangle in the assembled poster coordinate system. The last tile may therefore be physically narrower/shorter than an interior tile without distortion.

Source crop coordinates are not generated by the model and are not persisted as planner-authored values. At render time, deterministic code maps the decoded source into the complete poster target using Contain or Cover, intersects that mapping with the tile's poster-space rectangle, and draws only that intersection.

Adjacent tile canvas rectangles overlap by the requested physical millimetres, so the repeated source area is deterministic. Registration marks are drawn only on shared edges when requested. Tile labels identify row, column and sequential tile number.

A target with only width or height may derive the missing dimension from trusted inspected source aspect ratio. When neither a target/grid nor trusted physical source size is available, the planner must ask for size rather than invent one.

Poster tiles are simplex and currently operate on one selected source page. Poster is not combined with N-up, booklet, physical scaling, page placement, general crop, or Canvas in this slice.

The Windows printer path remains unchanged: each PosterTile output page is still an ordinary physical A4-or-smaller raster submitted through the existing spooler path.


## ADR-034 - Variable-size general composition compiles to deterministic multi-page Canvas placements
Status: accepted

General mixed-size print requests are represented by `VariableItemsSpec` at the PrintPlan layer.

Each item declares only semantic input:

- approved source index/page
- requested physical width/height
- copies
- fit
- whether 90-degree rotation is allowed

The AI does not author X/Y coordinates, page numbers for output sheets, z-order, or packing decisions.

Deterministic code resolves missing dimensions only from trusted inspected metadata. A single width/height may derive the other dimension from source aspect ratio. No explicit dimensions may preserve a trusted physical PDF page size. If neither rule can determine a physical size, planning must ask instead of guessing.

The compiler expands physical copies and uses a deterministic shelf-based packer inside paper margins/gaps. Items may rotate independently when allowed. When one physical sheet is full, packing continues on the next Canvas page.

`CanvasPlacementSpec.Page` extends the existing Canvas execution path to multiple output pages.

`CanvasPlacementSpec.UseRotatedFootprint` is separate from Smart Collage's arbitrary `RotationDegrees`. The packer uses the former for exact 90-degree physical footprint rotation, preventing variable-item packing from changing existing collage rotation semantics.

The compiled job still uses the existing Canvas renderer, preview, policy and Windows spooler path. No second mixed-size renderer is introduced.

Variable-size jobs are capped at 1000 physical placements. Complete repeated sets are emitted as separate collated batches.

This closes the final deterministic primitive gap family in the balanced M7 corpus without expanding PrintAI into non-print business workflows.


## ADR-035 - Antigravity CLI is the primary planner transport
Status: accepted

The target Windows machine already has Google Antigravity installed and authenticated. PrintAI therefore uses the local `agy.exe` headless interface as the primary natural-language planner transport instead of requiring an OpenAI-compatible endpoint and API key.

PrintAI does not own or persist Antigravity credentials. Authentication remains inside the Antigravity/Windows session.

The common-case planner path is fast-first rather than router-first. A fast/low-effort AGY model produces the final structured PrintJobSpec 1.0 or PrintPlan 2.0 proposal directly. Existing strict parsers, source binding and deterministic validators inspect that result. Only invalid, low-confidence or transport-failed fast passes escalate once to a configurable stronger AGY model.

A clarification question is a valid planner result and does not automatically escalate; stronger reasoning must not be used to guess a missing material fact.

The AGY process runs with terminal sandboxing and PrintAI never passes `--dangerously-skip-permissions`. Prompts instruct AGY not to call tools, shell commands, edit files or control printing.

Regardless of model tier, AGY remains a proposal layer. It may not issue arbitrary print commands, address the Windows spooler, invent local source paths, calculate printer/device coordinates, or bypass PrintJobSpec/PrintPlan validation, preview or policy gates.

Model slugs and effort are configuration values because the Antigravity catalog can evolve without changing the print-domain contracts.

See `docs/ANTIGRAVITY_PLANNER.md`.


## ADR-036 - Smart Collage keeps deterministic fallback during AGY migration
Status: accepted

The former Smart Collage AI path used a generic multimodal chat-completions endpoint and uploaded compact image thumbnails.

The product direction no longer accepts a hidden separate AI API-key requirement. Until the installed AGY CLI local-image/file interaction is verified on the target Windows machine, Smart Collage AI analysis is disabled on the AGY planner path and the existing deterministic template library remains available.

This is an intentional capability fallback, not permission to reintroduce the generic API-key transport as a required production dependency.
