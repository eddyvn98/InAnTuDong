# Libraries and Dependency Policy

Prefer proven libraries/platform APIs for commodity functionality.

A dependency should have a clear purpose, compatible license, active-enough maintenance, and a replacement boundary.

## Current choices

### SkiaSharp 4.150.1

Used by `PrintAI.Rendering` for image decode/encode, crop/scale/rotate, preview rendering and raster drawing.

`SkiaSharp.NativeAssets.Linux.NoDependencies` is included so the Railway/Linux demo can execute the same rendering path as CI.

### PhotoSauce MagicScaler 0.15.0 + NativeCodecs.Libheif 1.19.5-preview1 + Libpng 1.6.44-preview1

Used only behind `PrintAI.ImageDecoding` to decode HEIC/HEIF into PNG bytes before the normal Skia inspection/rendering path.

The libheif and libpng packages include native binaries for Windows and Linux, so HEIC decoding does not depend on the optional Windows HEVC Store extension. The codec boundary is intentionally replaceable.

HEIC/HEVC patent licensing varies by jurisdiction. Distribution must remain subject to the product owner's licensing review before commercial release.

### PDFsharp 6.2.4

Used by `PrintAI.SourceInspection` to inspect PDF page count and physical page dimensions.

### PDFtoImage 5.4.0

Used by `PrintAI.Rendering` to rasterize individual PDF pages through PDFium into SkiaSharp bitmaps. The package targets .NET 10 and carries supported native PDFium runtimes for Windows/Linux/macOS.

PDF rendering stays behind the rendering boundary; Domain/Layout remain PDF-agnostic.

### MetadataExtractor 2.9.3

Used by `PrintAI.SourceInspection` to read available JPG/PNG metadata such as EXIF, JFIF and PNG metadata directories without hand-parsing file formats.

### Microsoft.Web.WebView2

Planned desktop UI host for the future Windows app.

### System.Drawing.Common 10.0.12 / System.Drawing.Printing

Used only in the Windows printing boundary to enumerate installed printers and inspect driver-advertised color, duplex, paper, resolution and printable-area capabilities.

The package is Windows-only for this product and is not referenced by the Railway/web path.

### Windows printing APIs

The capability probe is now implemented. The next step is spooler submission through the installed Windows driver rather than rebuilding the printing stack.

### Windows Image Acquisition (WIA)

The first scanner adapter is implemented through the Windows WIA COM API in `PrintAI.Windows.Scanning`.

WIA stays behind the shared `IScannerAdapter` boundary so a TWAIN adapter can replace it later without changing scan processing, recipes, planner or printing.

### Scan processing

`PrintAI.Scanning` uses SkiaSharp for deterministic scanned-image crop/deskew processing and PDFsharp for A4 PDF output from scanned pages.

## Intentionally custom

Small physical-unit/domain validation and deterministic layout rules remain custom because exact sizing is product-critical behavior and must be easy to test.

Do not add a complex packing library until mixed/irregular packing requirements justify it.

## Dependency boundary

Physical geometry stays in millimetres in Domain/Layout. Rendering libraries only receive converted pixel/device coordinates at the rendering boundary.
