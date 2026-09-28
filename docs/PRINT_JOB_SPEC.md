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
- `longEdge` — two-sided output bound/flipped on the long edge
- `shortEdge` — two-sided output bound/flipped on the short edge

The value does not require the printer to have automatic duplex hardware. Deterministic execution resolves the intent after printer selection:

- an automatic-duplex printer may execute one multi-page duplex batch through the driver,
- a simplex printer uses the guided manual-duplex front/reinsert/back workflow,
- a duplex request must never silently fall through to the one-page simplex submission path.

Manual duplex is an execution strategy for schema 1.0 and does not require a schema change.

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
