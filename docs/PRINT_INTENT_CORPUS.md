# Print Intent Corpus

## Purpose

This corpus measures how broadly PrintAI can understand and represent real print-only requests after the PrintPlan 2.0 integration.

It is deliberately a **balanced coverage corpus**, not a market-frequency sample. Each category contains the same number of requests so missing capability families remain visible instead of being hidden by many easy "print one A4 copy" examples.

Fixture:

`tests/PrintAI.Tests/Fixtures/print-intent-corpus.json`

Current schema version: `1.0`.

## Corpus shape

The first baseline contains **150 Vietnamese requests across 15 categories**, 10 requests per category:

1. simple document printing
2. page selection
3. mixed color rules
4. duplex rules
5. sets / copies / collation
6. multi-file sequencing
7. exact-size / repeated photo-label printing
8. material ambiguity that should trigger clarification
9. N-up / pages per sheet
10. scaling
11. crop / margin / position
12. paper / orientation / printer capability
13. booklet imposition
14. poster / tiled printing
15. variable-size items on one sheet

Each case records:

- stable ID
- Vietnamese natural-language request
- source profile
- expected coverage label
- required capability family
- explicit missing capability when the case is a gap

## Coverage labels

### supported

The request can be represented by current `PrintPlan 2.0`, `PrintJobSpec 1.0`, or an existing deterministic print workflow.

### supportedWithCapability

The intent is representable, but physical execution depends on the selected printer/driver advertising the requested paper or device capability.

Example: A5 or 4x6-inch paper.

### clarification

The request is materially ambiguous. Correct behavior is to ask a concise question rather than invent dimensions, quantity, page selection, or print rules.

### gap

The request cannot currently be represented faithfully enough for deterministic execution.

A gap case must name its missing capability.

## Baseline results

| Coverage | Cases | Share |
| --- | ---: | ---: |
| Supported | 92 | 61.3% |
| Supported with printer capability | 10 | 6.7% |
| Correct behavior is clarification | 10 | 6.7% |
| Missing primitive / gap | 38 | 25.3% |
| **Total** | **150** | **100%** |

Two useful interpretations:

- **Immediately/conditionally executable:** 102 / 150 = 68.0%.
- **Semantically handled correctly, including asking when ambiguous:** 112 / 150 = 74.7%.

Do not interpret these percentages as real customer traffic share. The corpus gives every category equal weight.

## Gap families

The 38 current gap cases break down as follows:

| Gap family | Cases | Current limitation |
| --- | ---: | --- |
| Crop / margin / position | 8 | no asymmetric margin, crop region, anchor or content offset primitive |
| Booklet imposition | 10 | no booklet page-signature/reordering engine |
| Poster tiling | 10 | no oversized target canvas split across physical sheets |
| Variable item sizes | 10 | PrintJobSpec grid remains uniform-size; Canvas is a dedicated collage path |

Crop / position gap details:

- auto crop for print: 1
- asymmetric margins: 2
- content offset: 1
- anchor positioning: 2
- explicit crop region: 2

## Regression protection

The corpus itself is validated in CI:

- exactly 150 cases
- unique stable IDs
- exactly 15 categories
- 10 requests per category
- known coverage labels only
- every gap names the missing primitive
- baseline coverage counts are intentional and version-controlled

Representative supported requests are also converted into deterministic `PrintPlanCompiler` regression tests, including:

- odd-page selection
- mixed color groups across complete sets
- multi-file/page ordering
- non-collated per-page copies
- simplex cover + duplex body separation
- mixed paper/orientation groups
- 2/4/6/8/9/16-up geometry
- N-up row-major ordering
- N-up + duplex
- N-up gap/border behavior
- shrink-only physical scaling
- explicit 50/80/125-percent physical scaling
- per-page physical-size preservation
- max-fit without trusted physical-size metadata

This is intentionally different from testing an LLM live in CI. The deterministic contract/compiler behavior is protected locally; live-model intent quality can be evaluated separately against the same corpus.

## Corpus-driven implementation progress

### Completed: General N-up

All 10 N-up corpus cases are now supported by deterministic execution.

Implemented behavior:

- explicit `NUpSpec.pagesPerSheet`
- supported values 2 / 4 / 6 / 8 / 9 / 16
- deterministic grid geometry
- deterministic auto-orientation
- explicit orientation override
- optional column override for presentation-style layouts
- row-major source order
- millimetre gap and margin
- optional border around every cell
- duplex composition remains independent
- preview and print reuse the normal Grid -> renderer -> spooler path

The N-up corpus cases moved from `gap` to `supported`, reducing deterministic gaps from 54 to 44.

Verification for this coverage change: CI #204 passed on Ubuntu + Windows and package-windows #148 passed, including packaged self-test and install lifecycle smoke checks.

### Completed: physical scaling

All 6 scaling gap cases now have deterministic execution semantics.

Implemented behavior:

- trusted PDF page dimensions flow from SourceInspector through the planner binder
- planner-supplied physical page sizes cannot override inspected values
- `ShrinkOnly` never enlarges a smaller source page
- `Percent` uses the inspected physical source size, not raster pixel dimensions
- explicit 80%, 125% and 1:2/50% requests are preserved
- `MaxFit` maximizes content inside the target while preserving all content
- per-page physical sizes remain independent inside mixed-size PDF jobs
- percentages that extend beyond the target are centered and deterministically clipped in preview
- scaling remains separate from Cover/crop intent

The 6 scaling cases moved from `gap` to `supported`, reducing deterministic gaps from 44 to 38.

### Next: crop / margin / position

The next implementation target is asymmetric margins, explicit crop regions, content offsets and anchor positioning.

After crop/margin/position:

1. booklet imposition
2. poster/tiled printing
3. variable-size general composition

Booklet and poster remain important, but both introduce stronger ordering/physical-sheet semantics than N-up. Variable-size layout is last because it changes the current uniform-grid execution contract most substantially.

## Corpus maintenance rules

- Keep IDs stable once merged.
- Add cases rather than rewriting old requests unless the old case is objectively malformed.
- When a gap becomes supported, change the case label and add deterministic tests for the new primitive.
- Update the baseline counts and this document in the same PR.
- Do not mark a request supported merely because an LLM can describe it; deterministic execution must exist.
- Do not use non-print business requirements in this corpus.

