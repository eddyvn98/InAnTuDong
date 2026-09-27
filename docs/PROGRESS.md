# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M3 - Interactive desktop AI flow complete; packaging next**

## Completed milestones

### M0 - Foundation
Complete.

### M1 - Deterministic A4 engine
Complete.

### M2 - Windows print path
Physically complete on Epson L3310, including exact-size PrintAI spooler verification.

## M3 completed capabilities

### Deterministic source/desktop pipeline

- WPF + WebView2 desktop shell
- file/folder input + drag/drop
- JPG/JPEG/PNG/PDF inspection
- PDFium rasterization
- multi-source / multi-source-page navigation
- deterministic A4 preview
- Windows printer selection
- current-page / job / all-source printing

### Provider-neutral AI planner

- versioned PrintJobSpec 1.0
- strict JSON parsing
- unknown-field rejection
- confidence/questions/warnings
- Safe / Smart / Auto policy
- provider-independent `IPlannerModelClient`

### Configurable desktop AI transport

PR #9 adds a chat-completions-compatible HTTP adapter.

Configuration can come from:

- desktop UI: endpoint + model + optional API key
- environment:
  - `PRINTAI_AI_ENDPOINT`
  - `PRINTAI_AI_MODEL`
  - `PRINTAI_AI_API_KEY`

API keys entered in the UI are process-memory only and are not persisted.

### Planner source allowlist

Planner output may reference only source paths already selected/inspected by the desktop app.

Invented or rewritten local file paths are rejected before rendering.

### Natural-language desktop flow

The desktop can now execute:

```text
select source
-> enter natural-language request
-> AI returns strict PrintJobSpec
-> bind source allowlist
-> deterministic validation
-> Safe/Smart/Auto policy
-> render preview
-> optional deterministic user edits
-> print
```

### Deterministic job editor

The user can edit:

- Grid / ExactSize
- Contain / Cover
- item width/height in mm
- gap
- margin
- source copies
- rotation
- cut marks

Edits are validated by the deterministic domain validator and immediately re-rendered.

### Output pagination

Source pagination and physical A4 output pagination are now separate.

Example:

- one JPG source page
- 20 copies at 40 x 60 mm
- layout generates 2 physical A4 output pages

The desktop exposes both A4 pages and **Print toàn bộ job hiện tại** submits all physical output pages in order.

### Job history

New `PrintAI.History` project stores a capped local history.

- newest first
- maximum 100 entries
- plan/print status
- printer/job metadata
- malformed history fails closed to an empty list
- API keys are never stored
- UI shows the latest 20 entries
- user can clear local history

## Verification

PR #9 implementation CI run #50 passed all meaningful code checkpoints before the final documentation commit:

Shared Ubuntu/Windows suite:

- 35 tests pass
- planner transport request/response tests
- transport HTTP failure behavior
- planner source allowlist tests
- job-history tests
- multi-output renderer test
- all previous planner/layout/PDF/render tests

Windows:

- 5 printer tests pass
- printer probe builds
- printer probe smoke-run succeeds
- WebView2 desktop build succeeds

The first iterations exposed only integration/import fixture issues, all corrected before merge.

## Remaining M3 work

1. Package/publish the Windows desktop app for normal use.
2. Add an installable/release artifact in CI.
3. Then move to M4 scan + recipes.

## Important execution boundary

AI still cannot:

- calculate final device coordinates
- bypass PrintJobSpec validation
- access unapproved source paths
- bypass Safe/Smart/Auto policy
- directly call the Windows printer adapter

Execution remains:

source -> inspection -> planner/spec -> allowlist -> validation -> layout -> preview -> policy/user approval -> Windows print adapter.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and relevant specs first. Update this file before ending a work session.
