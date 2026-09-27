# PrintJobSpec

`PrintJobSpec` is the canonical boundary between AI intent understanding and deterministic execution.

## Rules

- physical values use millimetres
- A4 defaults to 210 x 297 mm
- source references remain explicit
- AI describes intent; layout engine calculates placements
- do not silently guess values that materially change physical output

## Example

```json
{
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

## Validation invariants

- width/height > 0
- paper <= configured maximum
- copies >= 1
- at least one source
- margin/gap >= 0
- layout must fit printable bounds
- unsupported capabilities surface before print

A `schemaVersion` will be added before this contract becomes externally versioned.
