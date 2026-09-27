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

## ADR-015 - Desktop talks to a PrintAI planner gateway, not a vendor API
Status: accepted

The desktop model transport is configured with `PRINTAI_PLANNER_URL` and optional `PRINTAI_PLANNER_TOKEN`. It posts the PrintAI-owned `PlannerModelRequest` contract and expects the raw strict planner JSON envelope.

Provider credentials and provider-specific request/response formats stay behind that gateway. This keeps desktop/core independent from OpenAI, Anthropic, Google, OpenRouter or local-model APIs.
