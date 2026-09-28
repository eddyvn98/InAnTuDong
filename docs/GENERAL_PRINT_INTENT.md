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

The model may not invent a path or change page count.

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

The balanced real-request corpus now reflects the General N-up implementation:

- 150 Vietnamese print requests
- 15 categories, 10 cases each
- 86 directly supported
- 10 supported subject to printer/driver capability
- 10 correctly require clarification
- 44 expose missing deterministic primitives

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

The next capabilities are now prioritized from the remaining gaps:

1. richer scaling: shrink-only and custom percent
2. crop, anchor, offset and asymmetric margins
3. booklet imposition
4. poster/tiled printing
5. variable-size independent items in one sheet

These are print features. Business/order-management features remain out of scope.

## Compatibility rule

Do not migrate existing working workflows to PrintPlan 2.0 merely for consistency.

Use PrintJobSpec 1.0 directly for simple uniform jobs. Use PrintPlan 2.0 when a request requires multiple page/source/rule groups or explicit sets/collation.

This keeps the current execution layer stable while expanding natural-language print coverage.


Corpus rule: a case is not considered supported merely because the LLM can describe it. A deterministic validated execution path must exist.
