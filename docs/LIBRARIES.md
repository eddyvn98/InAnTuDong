# Libraries and Dependency Policy

Prefer proven libraries/platform APIs for commodity functionality.

A dependency should have a clear purpose, compatible license, active-enough maintenance, and a replacement boundary.

## Current choices

### SkiaSharp 4.150.1

Used by `PrintAI.Rendering` for image decode/encode, crop/scale/rotate, preview rendering and raster drawing.

`SkiaSharp.NativeAssets.Linux.NoDependencies` is included so the Railway/Linux demo can execute the same rendering path as CI.

### PDFsharp 6.2.4

Used by `PrintAI.SourceInspection` to inspect PDF page count and physical page dimensions.

It is not currently the PDF rasterization engine. Rendering imported PDF pages remains a separate future capability.

### MetadataExtractor 2.9.3

Used by `PrintAI.SourceInspection` to read available JPG/PNG metadata such as EXIF, JFIF and PNG metadata directories without hand-parsing file formats.

### Microsoft.Web.WebView2

Planned desktop UI host for the future Windows app.

### System.Drawing.Common 10.0.12 / System.Drawing.Printing

Used only in the Windows printing boundary to enumerate installed printers and inspect driver-advertised color, duplex, paper, resolution and printable-area capabilities.

The package is Windows-only for this product and is not referenced by the Railway/web path.

### Windows printing APIs

The capability probe is now implemented. The next step is spooler submission through the installed Windows driver rather than rebuilding the printing stack.

### Scanner adapter

Prefer WIA for the first Epson L3310 implementation; keep the boundary replaceable with TWAIN if needed.

## Intentionally custom

Small physical-unit/domain validation and deterministic layout rules remain custom because exact sizing is product-critical behavior and must be easy to test.

Do not add a complex packing library until mixed/irregular packing requirements justify it.

## Dependency boundary

Physical geometry stays in millimetres in Domain/Layout. Rendering libraries only receive converted pixel/device coordinates at the rendering boundary.
