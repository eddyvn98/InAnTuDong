# Architecture

## Core flow

```
Input
 -> Source Inspector
 -> Planning
      -> simple uniform request: PrintJobSpec 1.0
      -> complex multi-rule request: PrintPlan 2.0
           -> PrintPlan Validator / Source Binder
           -> PrintPlan Compiler
           -> one or more PrintJobSpec 1.0 batches
 -> PrintJobSpec Validator
 -> Layout Engine
 -> Renderer
 -> Preview
 -> Policy Gate
 -> Windows Print Adapter
 -> Driver / Spooler
 -> Printer
```

Scanning enters the same pipeline after acquisition.

## Projects

### PrintAI.Domain

Pure print-domain contract:

- physical units
- `PrintJobSpec 1.0` execution job
- `PrintPlan 2.0` high-level multi-rule print intent
- validation and compilation from plan to executable jobs
- printer-neutral intent

No UI, AI SDK, Epson API, or spooler dependency.

### PrintAI.Layout

Deterministic placement:

- A4 geometry
- margins/gaps
- repetition
- optional 90-degree rotation
- future cut marks/packing

AI never supplies final pixel/device coordinates. `PrintPlan 2.0` may select pages and shared print rules, but deterministic code still owns executable geometry.

### PrintAI.Web

Cross-platform demo/test surface.

Purpose:

- exercise domain/layout without a Windows machine
- provide Railway health/demo verification
- later host a richer web UI reusable inside WebView2

It is not the final printer-control process.

### Future Windows Agent

Windows-only boundary for:

- installed printers
- driver capabilities
- printable region
- spooler submission
- scanner adapters
- native WebView2 desktop host

## Safety boundary

No job reaches printer execution without:

1. structured deserialization,
2. domain validation,
3. deterministic layout,
4. policy decision.

Policy result is one of:

- Direct
- PreviewRequired
- QuestionRequired
- Rejected

## Extensibility

Use adapters for AI provider, scanner backend, printer backend, OCR, office conversion, and PDF rendering.

Epson L3310 remains a profile rather than a type baked into the domain.


## General Print Intent

`PrintJobSpec 1.0` remains the stable execution boundary. `PrintPlan 2.0` sits above it for requests that need different rules across pages/files, explicit sets/collation, or ordered multi-file output. The compiler decomposes a validated plan into ordinary `PrintJobSpec 1.0` batches, so existing layout/rendering/policy/spooler code remains reusable.

See `docs/GENERAL_PRINT_INTENT.md`.
