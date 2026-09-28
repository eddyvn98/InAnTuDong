# PrintJobSpec

`PrintJobSpec` is the canonical boundary between AI intent understanding and deterministic execution.

Current schema version: `1.0`.

## Rules

- physical values use millimetres
- A4 defaults to 210 x 297 mm
- source references remain explicit
- compiler-generated virtual blank pages may use `isBlank=true` with no file path
- compiler-generated poster tiles may carry `posterTile` execution metadata
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

## Virtual blank sources

Booklet padding may require physical blank pages to reach a multiple of four logical pages.

The compiler represents these as:

```json
{
  "path": "",
  "copies": 1,
  "pageIndex": 0,
  "isBlank": true
}
```

Rules:

- virtual blanks are created only by deterministic compilation,
- they do not reference or invent an approved file,
- the renderer creates a white raster locally,
- ordinary non-blank sources still require an explicit path,
- planner/user input should express booklet intent through `PrintPlan 2.0`, not manually author blank source entries.

This keeps booklet padding inside the executable job boundary without creating temporary fake source files.

## Poster tile sources

Poster tiling is authored at the PrintPlan 2.0 level and compiled into ordinary executable sources carrying deterministic poster-space metadata.

A compiled poster tile source includes:

- source path/page reference
- tile row/column and total grid size
- assembled poster target width/height
- the tile rectangle inside that poster canvas
- overlap amount
- Contain/Cover mapping mode
- optional registration-mark flag
- optional tile-label flag

The executable layout mode is `posterTile`. Every poster source produces one physical output page, and the final row/column may use a smaller placement size than interior tiles.

The model does not author per-tile crop coordinates. Renderer code derives the exact source rectangle from the decoded source aspect and poster-level mapping.

Poster tiles are simplex. Complete repeated posters are represented as separate compiled batches rather than per-tile source copies.

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
- non-blank sources require a path
- virtual blank sources must not reference a path/page
- PosterTile sources require valid poster-space metadata
- PosterTile layout is simplex and one physical output page per tile

`schemaVersion` is mandatory for AI planner output. The current runtime accepts only `1.0`; unsupported versions are rejected before layout or printing.
