# M7 Real Print Intent Corpus

## Purpose

The corpus prevents PrintAI from expanding by intuition alone.

It contains 120 Vietnamese print requests that resemble what a user can actually ask at the print desk. The corpus stays strictly inside printing and does not include customer management, pricing, payment, stock, delivery or post-print business workflows.

File:

`tests/fixtures/general-print-intent-corpus.json`

## Coverage

The first corpus has 12 categories with 10 requests each:

1. basic printing
2. page selection
3. sets and collation
4. multi-source ordering
5. mixed color
6. duplex
7. N-up/pages-per-sheet
8. scaling
9. crop/margin/position
10. paper/orientation
11. booklet/poster
12. repeat/variable-size items

Each case records:

- stable ID
- category
- Vietnamese request
- source page counts
- intent feature tags
- `expectedSupport`

`expectedSupport=supported` means the current PrintPlan 2.0 contract should be able to express the request.

`expectedSupport=planned` means the request intentionally represents a known gap such as general N-up, custom scaling, crop/position, booklet, poster tiling or variable-size items.

This flag is a deterministic product-coverage statement. It is not a claim that an arbitrary LLM will interpret every supported request correctly.

## CI guard

`GeneralPrintIntentCorpusTests` checks that:

- the corpus parses
- exactly 120 stable cases exist
- IDs are unique
- all 12 categories remain represented
- each category currently has 10 cases
- source page counts and feature tags are valid
- supported/planned labels use known values
- the corpus does not silently shrink below its intended supported/planned coverage

CI also builds the live evaluator tool so it cannot rot even though network/provider calls are not executed in CI.

## Live model evaluator

Project:

`tools/PrintAI.IntentEval`

The evaluator uses the same provider-neutral `GeneralPrintPlanner`, strict parser, source binder, validator and compiler as the application.

Configuration:

```text
PRINTAI_AI_ENDPOINT=https://.../chat/completions
PRINTAI_AI_MODEL=model-name
PRINTAI_AI_API_KEY=optional
```

Run a small sample from the repository root:

```bash
dotnet run --project tools/PrintAI.IntentEval/PrintAI.IntentEval.csproj -- --limit 10
```

Run all currently supported cases:

```bash
dotnet run --project tools/PrintAI.IntentEval/PrintAI.IntentEval.csproj -- --all --supported-only
```

Run one case:

```bash
dotnet run --project tools/PrintAI.IntentEval/PrintAI.IntentEval.csproj -- --case mixed-color-001
```

The evaluator reports:

- `PASS`: structurally valid PrintPlan + compile success + deterministic feature expectations matched
- `FAIL`: the plan compiled but missed a checked semantic feature
- `ERROR`: transport, JSON, source-binding, validation or compile failure
- `TARGET`: a planned capability case; useful for exploration but not counted as currently supported

The evaluator deliberately does not send real user files. It creates synthetic PDF source descriptors with only page counts.

## Checked semantic features

The first evaluator can deterministically verify important intent dimensions including:

- multiple sources
- output-group count
- explicit page ranges
- multiple ranges
- exclusions
- odd/even parity
- sets
- collated vs uncollated output
- color/grayscale and mixed color
- long-edge/short-edge/simplex and mixed duplex
- draft/high quality
- portrait/landscape
- A4 and 4x6-inch paper
- preview-required policy

Some tags such as natural-language source ordering are not yet automatically scored. They remain visible in the corpus for manual inspection and future evaluator hardening.

## How the corpus drives M7

Do not mark a planned feature complete only because one prompt works once.

For a new primitive:

1. implement the deterministic domain/layout/execution behavior
2. move the relevant corpus cases from `planned` to `supported`
3. add deterministic unit/regression tests
4. run the live evaluator against the intended model/provider
5. inspect semantic failures before expanding to the next primitive

Current priority after the corpus foundation:

1. run supported cases against the configured production model
2. inspect failure clusters
3. implement general N-up/pages-per-sheet
4. then scaling
5. then crop/margins/position
6. only then booklet/poster/variable-size placement
