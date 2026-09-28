# General Print Intent / PrintPlan 2.0

## Scope

PrintAI remains a print-only product.

In scope:

- understand natural-language print requests
- select and order source pages
- apply different print rules to different page groups
- handle copies, complete sets and collation
- combine multiple approved files/pages
- choose paper, orientation, scaling/layout, color and duplex intent
- compile high-level intent into deterministic executable print jobs
- preview, validate and submit those jobs through the existing printer pipeline

Out of scope:

- customer/CRM management
- quotations and pricing
- payments
- inventory
- delivery
- production/order management unrelated to printer execution
- lamination, binding or other post-print business workflows

## Problem

PrintJobSpec 1.0 is a good execution contract but is intentionally uniform: one job has one paper/layout/print rule set.

Real print requests often contain multiple rules:

- "Trang 1 màu, còn lại trắng đen."
- "Trang bìa một mặt, phần còn lại hai mặt."
- "In 5 bộ, ghép file A rồi file B."
- "Chỉ in trang lẻ, bỏ trang 3."
- "Trang 1-5 file A rồi trang 2 file B."

These requests should not force PrintJobSpec 1.0 to become an unbounded all-purpose schema.

## Architecture

PrintPlan 2.0 is a high-level, printer-neutral print-intent contract.

```
files + natural-language request
        |
        v
source inspection
        |
        v
General Print Planner
        |
        v
PrintPlan 2.0
        |
        v
strict validation + source allowlist
        |
        v
PrintPlanCompiler
        |
        +--> PrintJobSpec 1.0 batch 1
        +--> PrintJobSpec 1.0 batch 2
        +--> ...
        |
        v
existing layout / preview / policy / spooler pipeline
```

PrintJobSpec 1.0 remains the execution boundary. Existing deterministic layout, rendering, duplex and Windows printing code is not replaced.

## PrintPlan 2.0 model

### Sources

Each source stores:

- approved file path
- inspected page count
- trusted per-page physical size in millimetres when the source format exposes it

For PDF, physical page size comes from deterministic source inspection. The model may not invent or change path, page count, or physical page size. The source binder replaces planner-side metadata with the inspected values before compilation.

### Page selection

A selection references one source and supports:

- one or more inclusive page ranges
- optional excluded ranges
- all / odd / even parity

Page numbers in PrintPlan are one-based because they represent user-facing document pages. Compilation converts them to the zero-based PageIndex used by PrintJobSpec 1.0.

### Output groups

An output group is a sequence of selected pages that share one print rule:

- paper
- layout
- color mode
- quality
- duplex
- sets
- collate
- execution sequence

Different rules become different groups.

Example:

```text
Request:
"In 3 bộ. Trang 1 màu một mặt, trang 2-20 trắng đen hai mặt."

PrintPlan:
group 0: page 1, color, simplex, sets=3
group 1: pages 2-20, grayscale, long-edge duplex, sets=3
```

### Sets and collation

`sets` means repeated output quantity.

When `collate=true`, the compiler emits complete repeated batches so source order is preserved per set.

When `collate=false`, the compiler may express the quantity as per-source copies inside one executable job.

This distinction prevents "5 bộ" from being treated as an ambiguous generic copy count.

## Validation rules

The current PrintPlan 2.0 foundation requires:

- schemaVersion = 2.0
- at least one approved source
- source pageCount >= 1
- at least one output group
- unique output-group sequence values
- valid source indexes
- page ranges inside inspected page counts
- page selections resolve to at least one page
- sets >= 1

Every compiled PrintJobSpec is validated again by the existing PrintJobValidator before execution.

## Current implementation

Implemented on branch `m7-general-print-intent`:

- PrintPlan 2.0 domain model
- page range/include/exclude/parity resolution
- output group sequencing
- sets vs collate semantics
- cross-group complete-set ordering: set 1 cover -> body, then set 2 cover -> body, instead of printing all covers first
- compiler to one or more PrintJobSpec 1.0 jobs
- strict JSON parser for the general planner
- explicit PrintPlan JSON shape/example in the model instruction
- source-path and page-count binding
- provider-neutral GeneralPrintPlanner
- General PrintPlan policy gating through Safe / Smart / Auto
- desktop AI request path now uses GeneralPrintPlanner
- compiled-batch navigation and per-batch preview in the desktop UI
- safe full-plan execution when every batch is simplex
- duplex plans remain intentionally batch-by-batch so manual reinsert state cannot accidentally advance into another batch
- Windows printing now honors `PrintSettings.ColorMode`; grayscale intent is passed to the driver instead of always enabling color
- manual-duplex pending state persists color mode for the back pass
- switching to manual workflows/recipes/layouts clears stale compiled-plan state
- tests for mixed color/duplex, complete-set ordering, collated/non-collated copies, page filtering, parser/source safety, policy gates and Windows color-mode mapping

Verification for the desktop integration slice:

- CI #191: Ubuntu + Windows success
- package-windows #135: success
- Windows desktop build: success
- packaged self-test: success
- install / upgrade / rollback / uninstall smoke flow: success

## Corpus-driven M7 extensions

The balanced real-request corpus now reflects General N-up, physical scaling, and crop/placement:

- 150 Vietnamese print requests
- 15 categories, 10 cases each
- 100 directly supported
- 10 supported subject to printer/driver capability
- 10 correctly require clarification
- 30 expose missing deterministic primitives

See `docs/PRINT_INTENT_CORPUS.md`.

### General N-up

General N-up is implemented as a high-level `NUpSpec` on a PrintPlan output group.

Supported `pagesPerSheet` values:

- 2
- 4
- 6
- 8
- 9
- 16

Deterministic code resolves N-up into an ordinary `LayoutMode.Grid` execution job. The AI does not calculate physical cell width/height.

N-up supports:

- row-major source-page ordering
- automatic deterministic paper orientation
- explicit orientation override
- optional explicit column count for clearly implied grids, such as 8 presentation slides in 2 columns x 4 rows
- gap and margin in millimetres
- optional border around each page cell
- Contain/Cover fit
- duplex without changing duplex semantics
- incomplete final sheets when the selected page count is not divisible by pages-per-sheet

The existing grid renderer, preview pipeline and Windows spooler remain unchanged below the compiled PrintJobSpec boundary.

### Physical scaling

Physical scaling is represented separately from `FitMode`.

Supported modes:

- `MaxFit`: preserve all content and use the largest size that fits the target placement.
- `ShrinkOnly`: preserve the source's inspected physical size unless it must shrink to fit; never upscale.
- `Percent`: render at an explicit percentage of the source's inspected physical page size.

Examples:

- 80% -> `Percent(80)`
- 125% -> `Percent(125)`
- 1:2 -> `Percent(50)`
- "chỉ thu nhỏ nếu lớn hơn" -> `ShrinkOnly`
- "phóng tối đa nhưng vẫn giữ toàn bộ" -> `MaxFit`

`ShrinkOnly` and `Percent` require trusted physical source dimensions. PDF page dimensions are supplied by SourceInspector and rebound deterministically after AI planning. If trustworthy physical size is unavailable, the planner must ask instead of guessing.

Percent scaling is centered and preserves physical proportions. A requested percentage may extend beyond the target placement; preview then shows the deterministic clipping. `Cover` remains a separate crop/fill intent.

Physical scaling verification: final PR head CI #213 and package-windows #157 passed.

### Page placement and source crop

General page placement is represented separately from source cropping.

`PagePlacementSpec` supports:

- asymmetric left/top/right/bottom margins in millimetres
- anchors: center, top, bottom, left, right, four corners
- X/Y physical offsets in millimetres
- proportional shrink-to-fit when requested margins reduce the available page area

Offset convention:

- positive X moves right
- negative X moves left
- positive Y moves down
- negative Y moves up

Placement currently applies to `ExactSize` jobs and is intentionally not combined with General N-up.

`SourceCropSpec` supports:

- `AutoTrimWhite`: deterministic white-border detection before fit/render
- `CenterToTargetAspect`: centered crop matching the target placement aspect
- `EdgesMm`: explicit left/top/right/bottom crop in physical millimetres

Millimetre edge crop requires trusted source physical dimensions. For PDF those dimensions are supplied by SourceInspector and rebound at the planner boundary.

Source crop is applied before Contain/Cover mapping. In this slice it is not combined with physical scaling, General N-up, or Canvas. Page placement and source crop may be combined.

Examples:

- "chừa lề trái 20 mm" -> asymmetric page margins
- "đưa nội dung lên 5 mm" -> `offsetYMm=-5`
- "căn sát mép phải" -> right anchor
- "căn xuống góc dưới bên phải" -> bottom-right anchor
- "cắt phần trắng xung quanh" -> `AutoTrimWhite`
- "chỉ lấy phần giữa" -> `CenterToTargetAspect`
- "cắt 10 mm mép trên" -> `EdgesMm(top=10)`

Crop/placement verification: CI #218 passed on Ubuntu + Windows with 191/191 shared tests, and package-windows #162 passed.

The next capabilities are now prioritized from the remaining gaps:

1. booklet imposition
2. poster/tiled printing
3. variable-size independent items in one sheet

These are print features. Business/order-management features remain out of scope.

## Compatibility rule

Do not migrate existing working workflows to PrintPlan 2.0 merely for consistency.

Use PrintJobSpec 1.0 directly for simple uniform jobs. Use PrintPlan 2.0 when a request requires multiple page/source/rule groups or explicit sets/collation.

This keeps the current execution layer stable while expanding natural-language print coverage.


Corpus rule: a case is not considered supported merely because the LLM can describe it. A deterministic validated execution path must exist.
