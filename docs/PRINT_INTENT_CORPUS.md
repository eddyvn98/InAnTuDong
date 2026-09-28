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
| Supported | 76 | 50.7% |
| Supported with printer capability | 10 | 6.7% |
| Correct behavior is clarification | 10 | 6.7% |
| Missing primitive / gap | 54 | 36.0% |
| **Total** | **150** | **100%** |

Two useful interpretations:

- **Immediately/conditionally executable:** 86 / 150 = 57.3%.
- **Semantically handled correctly, including asking when ambiguous:** 96 / 150 = 64.0%.

Do not interpret these percentages as real customer traffic share. The corpus gives every category equal weight.

## Gap families

The 54 current gap cases break down as follows:

| Gap family | Cases | Current limitation |
| --- | ---: | --- |
| General N-up | 10 | no explicit pages-per-sheet semantic contract |
| Scaling | 6 | no shrink-only or arbitrary scale-percent primitive |
| Crop / margin / position | 8 | no asymmetric margin, crop region, anchor or content offset primitive |
| Booklet imposition | 10 | no booklet page-signature/reordering engine |
| Poster tiling | 10 | no oversized target canvas split across physical sheets |
| Variable item sizes | 10 | PrintJobSpec grid remains uniform-size; Canvas is a dedicated collage path |

Scaling gap details:

- shrink-only: 2
- custom scale percentage: 3
- maximize within a requested margin without upscaling ambiguity: 1

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

This is intentionally different from testing an LLM live in CI. The deterministic contract/compiler behavior is protected locally; live-model intent quality can be evaluated separately against the same corpus.

## Priority derived from the corpus

### Next: General N-up

N-up is the next implementation target because:

1. it closes a full 10-case gap family,
2. it is common print language ("2 trang/tờ", "4-up", "8 slide/tờ"),
3. it can reuse the existing deterministic grid/layout renderer,
4. it does not require the deeper scene-graph/schema changes needed by variable-size items,
5. it provides reusable foundations for later booklet/handout workflows.

Expected first slice:

- explicit `pagesPerSheet` intent
- deterministic candidate geometry for 2 / 4 / 6 / 8 / 9 / 16 pages per sheet
- source pages remain ordered
- portrait/landscape selection remains deterministic
- gap/border options
- duplex combines with N-up without changing duplex semantics
- preview uses the existing renderer

### After N-up

Recommended order:

1. richer scaling
2. asymmetric margins / crop / position
3. booklet imposition
4. poster/tiled printing
5. variable-size general composition

Booklet and poster remain important, but both introduce stronger ordering/physical-sheet semantics than N-up. Variable-size layout is last because it changes the current uniform-grid execution contract most substantially.

## Corpus maintenance rules

- Keep IDs stable once merged.
- Add cases rather than rewriting old requests unless the old case is objectively malformed.
- When a gap becomes supported, change the case label and add deterministic tests for the new primitive.
- Update the baseline counts and this document in the same PR.
- Do not mark a request supported merely because an LLM can describe it; deterministic execution must exist.
- Do not use non-print business requirements in this corpus.

