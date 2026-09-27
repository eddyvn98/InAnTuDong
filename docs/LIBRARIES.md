# Libraries and Dependency Policy

Prefer proven libraries/platform APIs for commodity functionality.

A dependency should have a clear purpose, compatible license, active-enough maintenance, and a replacement boundary.

## Planned choices

### Microsoft.Web.WebView2

Desktop UI host for the future Windows app.

### SkiaSharp

Image decode/encode, crop/scale/rotate, preview rendering, cut marks, and raster drawing.

### PDFsharp

PDF generation/manipulation where appropriate.

### Windows printing APIs

Use installed Windows printer drivers, capabilities and spooler rather than rebuilding the printing stack.

### Scanner adapter

Prefer WIA for the first Epson L3310 implementation; keep the boundary replaceable with TWAIN if needed.

## Intentionally custom

Small physical-unit/domain validation and deterministic layout rules remain custom because exact sizing is product-critical behavior and must be easy to test.

Do not add a complex packing library until mixed/irregular packing requirements justify it.

## Current bootstrap

The initial CI/web bootstrap intentionally has no production third-party rendering dependency yet. External rendering packages are added when their feature is implemented, not merely predeclared.
