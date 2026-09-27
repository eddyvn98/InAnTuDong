# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M1 - Deterministic A4 engine**

## Verified foundation

- Repository source-of-truth docs are in place.
- .NET 10 domain/layout/web skeleton is on `main`.
- GitHub Actions baseline CI is green on Ubuntu and Windows.
- Railway project `InAnTuDong` and service `printai-web` deploy from `eddyvn98/InAnTuDong/main`.
- Previous Railway deployment reached `SUCCESS` and ASP.NET Core listened on `0.0.0.0:8080`.
- Public domain: `https://printai-web-production.up.railway.app`.

## M1 implementation completed in PR #2

Branch: `feat/m1-rendering-inspection`

- `PrintAI.Layout`
  - grid/repeat placement
  - capacity-based automatic 90-degree rotation
  - exact-size mode preserving requested millimetres
  - exact-size one-item-per-page placement, centered on paper
  - contain/cover source-to-placement geometry
  - deterministic cut-mark geometry in millimetres
- `PrintAI.Rendering`
  - SkiaSharp A4 raster renderer
  - millimetres converted to pixels only at the rendering boundary
  - contain/cover drawing
  - 90-degree rotated placement rendering
  - cut-mark rendering
- `PrintAI.SourceInspection`
  - JPG/JPEG/PNG pixel dimensions and encoded orientation
  - JPG/PNG raw metadata via MetadataExtractor
  - PDF page count
  - PDF physical page dimensions in millimetres via PDFsharp
- `PrintAI.Web`
  - `/health`
  - `/api/demo-layout`
  - `/api/demo-preview.png`
  - browser now shows an actual SkiaSharp-generated A4 raster preview instead of CSS-only boxes

## Verification

Implementation commit CI run #10 is green on both:

- Ubuntu: restore -> 12 tests -> web build
- Windows: restore -> 12 tests -> web build

The CI sequence also caught and fixed:
- obsolete SkiaSharp bitmap sampling API
- a PNG test fixture that attempted inspection before its write stream was closed

## Exact next engineering tasks

1. Add golden/snapshot preview coverage for stable renderer output.
2. Start M2 Windows printer capability probe:
   - enumerate installed printers
   - identify Epson L3310
   - query paper/media/duplex/color capabilities
   - query printable area
3. Add an Epson L3310 calibration page with known physical rulers/boxes.
4. Print calibration output on real hardware and measure it with a ruler.
5. Only after physical sizing is verified, implement spooler submission.

## Deferred / known gaps

- `DpiX` / `DpiY` are modeled but not yet normalized from all possible EXIF/JFIF/PNG metadata combinations; raw metadata is retained so normalization can be added without losing source information.
- PDF metadata inspection is implemented, but PDF page rasterization is not.
- renderer demo currently uses one generated raster source; multi-source file loading/orchestration belongs to the source-to-render pipeline.
- exact-size correctness still requires physical printer calibration because driver printable-area behavior can vary by media/quality/borderless settings.
- real Windows spooler submission is not started.
- AI provider integration, WebView2 desktop shell, scanner control and recipes are not started.

## Session rule

Never continue from chat memory alone. Read this file, `AGENTS.md`, `ROADMAP.md`, `DECISIONS.md`, `LIBRARIES.md` and the relevant specs first. Update this file before ending a work session.
