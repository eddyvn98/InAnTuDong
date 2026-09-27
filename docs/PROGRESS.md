# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M3 - Planner contract/policy complete; desktop planning integration next**

## Completed milestones

### M0 - Foundation
Complete.

### M1 - Deterministic A4 engine
Complete:

- millimetre-based domain/layout
- validation
- grid/repeat + rotation
- exact-size mode
- contain/cover
- cut marks
- SkiaSharp A4 rendering
- JPG/PNG/PDF inspection
- golden preview coverage

### M2 - Windows print path
Physically complete on Epson L3310:

- installed printer enumeration/capabilities
- A4 printable area/hard margins
- calibrated Epson L3310 identity profile
- Windows spooler submission
- spooler job status/errors
- physical calibration measured correct
- exact-size PrintAI spooler output measured correct

## M3 deterministic desktop/source pipeline

Complete:

- WPF + WebView2 desktop shell
- file/folder input + drag/drop
- JPG/JPEG/PNG/PDF inspection
- PDFium rasterization through PDFtoImage
- flattened multi-source/multi-page navigation
- current-page and print-all flows
- 300 DPI render immediately before spooler submission
- explicit preview approval

## M3 planner + policy slice in PR #7

### Versioned PrintJobSpec

`PrintJobSpec` now carries `schemaVersion = "1.0"`.

Validation rejects:

- unsupported schema version
- empty job name
- null/empty source set
- existing invalid physical/layout/print values

### Provider-neutral planner

New `PrintAI.Planning` project.

`IPlannerModelClient` is the provider boundary. Core planning code does not depend on OpenAI, Anthropic, Google, OpenRouter or another vendor SDK.

`PrintPlanner` sends:

- a fixed system contract
- user natural-language request
- inspected source metadata

The model is allowed to propose intent only. It never receives authority to submit a print job or calculate printer/device coordinates.

### Strict structured parsing

Planner response must be a JSON envelope with:

- `job`
- `confidence`
- `questions`
- `warnings`

Strict parser behavior:

- JSON only
- case-sensitive camelCase contract
- unknown fields rejected
- enum integers rejected
- confidence must be 0..1
- schemaVersion must be 1.0
- resulting `PrintJobSpec` passes deterministic domain validation again

### Safe / Smart / Auto policy

`PrintPolicyEngine` is deterministic code.

- invalid spec -> Rejected
- material planner question -> QuestionRequired
- Safe -> PreviewRequired
- unverified printer -> PreviewRequired
- confidence below 0.90 or warnings -> PreviewRequired
- Smart unknown/unapproved job -> PreviewRequired
- Smart known verified high-confidence job -> Direct
- Auto can be Direct only after all gates pass

AI cannot override these decisions.

## Verification

PR #7 CI run #42 is green on Ubuntu and Windows.

Latest shared suite: 28 tests pass, including:

- strict planner JSON parsing
- unknown-field rejection
- schema-version rejection
- provider-neutral fake model client flow
- Safe/Smart/Auto policy decisions
- PDF rasterization and A4 preview
- all previous layout/rendering tests

Windows also passes:

- printer tests
- probe build/smoke-run
- WebView2 desktop build

## Exact next work

1. Add natural-language request input to desktop.
2. Add a configurable model-client transport without coupling core to a vendor.
3. Convert desktop source metadata into `PlanningSource`.
4. Run Plan -> strict parse -> policy decision.
5. Show proposed settings/questions/warnings before preview/print.
6. Allow deterministic user edits to layout/print settings.
7. Add job history.
8. Package/publish desktop app.

## Important design rule

AI may propose a `PrintJobSpec`, but never:

- computes final device coordinates
- bypasses deterministic validation
- decides printer execution outside the policy gate
- submits directly to the printer

Execution remains:

source -> inspection -> planner/spec -> validation -> deterministic layout -> preview -> policy/user approval -> Windows print adapter.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and relevant specs first. Update this file before ending a work session.
