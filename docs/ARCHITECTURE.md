# Architecture

## Core flow

```
Input
 -> Source Inspector
 -> AI Planner
 -> PrintJobSpec
 -> Validator
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

Pure business contract:

- physical units
- job specification
- validation
- printer-neutral intent

No UI, AI SDK, Epson API, or spooler dependency.

### PrintAI.Layout

Deterministic placement:

- A4 geometry
- margins/gaps
- repetition
- optional 90-degree rotation
- future cut marks/packing

AI never supplies final pixel/device coordinates.

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
