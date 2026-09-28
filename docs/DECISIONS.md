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
