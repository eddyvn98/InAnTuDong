# PrintJobSpec

`PrintJobSpec` is the canonical boundary between AI intent understanding and deterministic execution.

Current schema version: `1.0`.

## Rules

- physical values use millimetres
- A4 defaults to 210 x 297 mm
- source references remain explicit
- AI describes intent; layout engine calculates placements
- do not silently guess values that materially change physical output

## Example

```json
{
  "schemaVersion": "1.0",
  "jobName": "4x6 photos",
  "sources": [
    { "path": "D:/print/a.jpg", "copies": 2 },
    { "path": "D:/print/b.jpg", "copies": 2 }
  ],
  "paper": {
    "widthMm": 210,
    "heightMm": 297,
    "orientation": "portrait"
  },
  "layout": {
    "mode": "grid",
    "itemWidthMm": 40,
    "itemHeightMm": 60,
    "gapMm": 3,
    "marginMm": 5,
    "allowRotate": true,
    "cutMarks": true,
    "fit": "cover"
  },
  "print": {
    "copies": 1,
    "colorMode": "color",
    "quality": "high",
    "duplex": "off"
  },
  "policy": {
    "preview": "required"
  }
}
```

## Duplex semantics

`print.duplex` is printer-neutral user intent:

- `off` — one-sided output
- `longEdge` — two-sided output flipped/bound on the long edge
- `shortEdge` — two-sided output flipped/bound on the short edge

The value does not mean that the physical printer must have an automatic duplex unit. After printer selection, deterministic execution resolves the intent to either automatic duplex or guided manual duplex.

A runtime must never silently execute `longEdge` or `shortEdge` as simplex output. If automatic duplex is unavailable, use the manual-duplex path or block with an explicit actionable state.

Manual duplex does not require a schema change; it is an execution strategy for the existing schema 1.0 intent.

See `docs/MANUAL_DUPLEX.md`.

## Validation invariants

- width/height > 0
- paper <= configured maximum
- copies >= 1
- at least one source
- margin/gap >= 0
- layout must fit printable bounds
- unsupported capabilities surface before print

`schemaVersion` is mandatory for AI planner output. The current runtime accepts only `1.0`; unsupported versions are rejected before layout or printing.
