# Antigravity Fast/Deep Planner

## Goal

PrintAI uses the user's existing Google Antigravity desktop/CLI session on the same Windows machine.

PrintAI does **not** require an AI API key for natural-language print planning and does not run a local LLM.

The latency strategy borrows the decision-model idea used by systems such as Kev: keep the first model call narrow, structured and concise. PrintAI does not install or run Kev.

## Runtime flow

```
user request + inspected source metadata
  -> AGY fast model, low effort
       -> strict JSON
       -> local parser + PrintPlan/PrintJob validation
       -> confidence gate
            -> valid/confident: continue
            -> invalid/low confidence/transport failure: AGY deep model
  -> source allowlist binder
  -> deterministic compiler/layout
  -> preview/policy
  -> Windows spooler
```

The fast pass creates the final planner JSON directly. There is no separate routing-model call for ordinary requests. This avoids paying two AI round trips for simple jobs.

## Why this replaces the API-key planner

The target Windows machine already has Antigravity installed and authenticated with the user's subscription.

Primary transport is therefore `agy.exe`, not an OpenAI-compatible HTTP endpoint.

Credentials remain owned by Antigravity/Windows. PrintAI must not scrape, copy, persist or display Antigravity credentials.

## Discovery and configuration

PrintAI resolves AGY in this order:

1. explicit path passed by the desktop retry action;
2. `PRINTAI_AGY_PATH`;
3. `agy.exe` on `PATH`;
4. `agy` on `PATH`.

Planner tuning is configuration, not credentials:

```text
PRINTAI_AGY_PATH
PRINTAI_AGY_FAST_MODEL
PRINTAI_AGY_DEEP_MODEL
PRINTAI_AGY_FAST_EFFORT
PRINTAI_AGY_DEEP_EFFORT
PRINTAI_AGY_ESCALATE_BELOW
PRINTAI_AGY_TIMEOUT
```

Current defaults:

```text
fast model: gemini-3.8-flash-medium
deep model: gemini-3.8-flash-high
fast/deep effort: auto (use the effort encoded by the selected model; omit a conflicting `--effort` flag)
escalate below confidence: 0.80
timeout: 2m
```

Model names are deliberately configuration values. If Antigravity changes its available model catalog, PrintAI should update configuration rather than change the print-domain contract. Effort accepts `auto`, `low`, `medium`, or `high`; `auto` omits `--effort` for model slugs that encode their tier and models that do not support the flag. An explicit effort must match a tier encoded in the model slug.

## AGY invocation

The first implementation launches AGY headlessly and requests structured output:

```text
agy -p <prompt>
    --model <tier model>
    [--effort <explicit tier effort>]
    --output-format json
    --json-schema <schema>
    --print-timeout <timeout>
```

PrintAI reads only the CLI result envelope and extracts `structured_output` (or the response text fallback).

A later optimization may keep an AGY `stream-json` process warm. That optimization must preserve the same planner interface and safety boundary.

## Fast-pass design

The fast prompt is intentionally narrow:

- no explanation;
- no markdown;
- no shell/tool calls;
- only the supplied print request and inspected metadata;
- strict JSON;
- concise questions when material facts are missing;
- confidence reflects planning certainty.

For PrintJobSpec 1.0 and PrintPlan 2.0, the returned JSON is immediately parsed by the existing strict parsers.
The CLI schema is generated from those domain contracts, including nested fields and enum values, and closes objects against unknown properties. The parser and deterministic validators remain the final authority if a CLI/model ignores the schema.

The fast result is accepted when:

- the AGY transport succeeds;
- JSON parses;
- the domain plan validates;
- no material clarification question is required;
- confidence is at or above the configured threshold.

## Escalation

The deep model is called when the fast pass:

- exits with a transport failure;
- produces invalid planner JSON;
- produces a plan rejected by deterministic validation;
- reports confidence below the threshold.

Clarification is **not** automatically escalated. If the fast pass correctly determines that the user omitted a material fact, PrintAI should ask the question rather than spend a larger model call guessing.

## Safety boundary

AGY is a planner, never the print executor.

AGY must not:

- call PowerShell or arbitrary shell commands for printing;
- invoke the Windows spooler directly;
- rewrite source paths;
- invent unapproved local files;
- bypass PrintJobSpec/PrintPlan validation;
- bypass preview or policy gates.

PrintAI deterministic code owns:

- source allowlisting;
- physical dimensions;
- layout/packing;
- page imposition;
- printer capabilities;
- preview;
- spooler submission.

## Low-spec Windows target

No local language model is required.

The target machine runs:

- PrintAI/.NET;
- WebView2 local UI;
- `agy.exe`;
- printer/scanner drivers.

8 GB RAM can be workable for the PrintAI process itself; 16 GB RAM is the preferred baseline. A discrete GPU is not required by PrintAI's AGY integration.

## Current limitation: Smart Collage vision

The existing Smart Collage implementation previously uploaded 768 px thumbnails to an OpenAI-compatible multimodal endpoint.

That API-key path is no longer the primary planner design.

Until the local-image/file contract of the installed AGY CLI is verified on the target Windows machine, Smart Collage AI vision is disabled for AGY and the existing deterministic template fallback remains available.

Do not reintroduce a hidden API-key requirement just to keep Smart Collage vision enabled.

## Verification

Automated tests cover:

- fast result accepted without escalation;
- low-confidence fast result escalates to deep;
- fast transport failure escalates to deep.

Still required on the target Windows machine:

1. confirm `agy.exe` discovery;
2. confirm the signed-in Antigravity session works headlessly;
3. confirm the configured model names are available;
4. exercise simple PrintPlan 2.0 planning;
5. exercise a low-confidence/invalid fast case and verify deep escalation;
6. measure warm/cold latency;
7. decide whether persistent `stream-json` is worth adding.
