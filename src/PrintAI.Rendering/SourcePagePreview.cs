using PrintAI.Domain;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class SourcePagePreview
{
    public static byte[] RenderA4(
        PrintJobSpec job,
        string sourcePath,
        int pageIndex,
        int dpi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        using var source = Decode(sourcePath, pageIndex, dpi);
        var layout = LayoutEngine.Layout(job);
        return A4PreviewRenderer.RenderPng(
            job,
            layout,
            source,
            page: 0,
            dpi: dpi);
    }

    private static SKBitmap Decode(
        string sourcePath,
        int pageIndex,
        int dpi)
    {
        var extension = Path.GetExtension(sourcePath);

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return PdfPageRasterizer.RenderPage(sourcePath, pageIndex, dpi);

        if (pageIndex != 0)
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                "Raster image sources only have page index 0.");

        return SKBitmap.Decode(sourcePath)
            ?? throw new InvalidDataException("The raster source could not be decoded.");
    }
}
