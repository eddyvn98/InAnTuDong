# Auto Layout 4x6

## Goal

Allow a user to select 1-6 source pages/images, choose 4x6 inch paper, generate several deterministic layout alternatives, preview them, choose one, and print that exact layout.

The primary user story is:

```text
select 3 images
  -> choose 4x6 inch
  -> generate alternatives
  -> inspect 3-4 previews
  -> choose one
  -> print
```

4x6 inch is represented as **101.6 x 152.4 mm**. Physical dimensions remain millimetre-based throughout the domain model.

## Product rules

- Auto layout is deterministic local code. The LLM may interpret a future natural-language request, but it does not calculate final coordinates.
- The first slice supports 1-6 selected source pages.
- The generator tries portrait and landscape paper orientation.
- Candidate layouts use uniform grid cells so they remain compatible with PrintJobSpec schema 1.0 and the existing deterministic layout engine.
- Up to four candidates are returned.
- The user can prefer:
  - Balanced
  - MinCrop
  - Fill
- Every candidate is previewed before printing.
- Selecting a candidate turns it into the normal active PrintJobSpec, so the existing preview, history and print path remain authoritative.

## Candidate generation

For N selected items, the generator enumerates row/column grids whose capacity is at least N, in both paper orientations.

Each candidate is scored using deterministic factors:

- used paper area
- unused grid cells
- extreme cell aspect ratios
- row/column imbalance

The preference changes fit mode and scoring weight:

- **Balanced**: Contain, with stronger layout-balance weighting.
- **MinCrop**: Contain, with stronger penalty for extreme cell shapes.
- **Fill**: Cover, with stronger paper-coverage weighting.

This intentionally avoids an AI call per candidate and keeps repeated use fast and reproducible.

## Printing

Preview rendering already follows PaperSpec dimensions. Windows printing must also use the job paper size.

For non-A4 output, the Windows printer driver must advertise a matching paper size. PrintAI must fail clearly instead of silently scaling a 4x6 job onto A4.

The initial matching tolerance is 2 mm to account for driver rounding.

## Acceptance criteria

1. Three JPG/PNG/HEIC/PDF source pages can be selected.
2. 4x6 inch produces PaperSpec 101.6 x 152.4 mm.
3. Three selected items produce at least three distinct candidate layouts.
4. Candidate previews have the correct paper aspect ratio and do not place content outside the paper bounds.
5. Choosing a candidate updates the main preview and active print job.
6. Printing submits the selected paper size and orientation to the Windows driver.
7. A driver without the requested paper size returns a clear error instead of printing on a different size.
8. Existing A4 jobs continue to use the same physical A4 path.
9. Shared tests cover candidate count, bounds and 4x6 paper dimensions.
10. Windows CI continues to compile and exercise the spooler helper surface.

## Current scope limitation

Schema 1.0 supports uniform physical item geometry inside a job. Therefore this slice does not yet generate irregular collage geometry such as one large photo plus two smaller photos with different physical sizes.

That can be added later with an explicit custom-placement schema rather than hiding non-deterministic geometry inside the AI layer.
