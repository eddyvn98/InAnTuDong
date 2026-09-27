using PrintAI.Domain;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class SourceJobRenderer
{
    public static int GetOutputPageCount(PrintJobSpec job)
    {
        var layout = LayoutEngine.Layout(job);
        return layout.Placements.Count == 0
            ? 0
            : layout.Placements.Max(p => p.Page) + 1;
    }

    public static byte[] RenderA4(
        PrintJobSpec job,
        string sourcePath,
        int sourcePageIndex,
        int outputPageIndex,
        int dpi)
    {
        var layout = LayoutEngine.Layout(job);
        var pageCount = layout.Placements.Count == 0
            ? 0
            : layout.Placements.Max(p => p.Page) + 1;

        if (outputPageIndex < 0 || outputPageIndex >= pageCount)
            throw new ArgumentOutOfRangeException(nameof(outputPageIndex));

        using var source = Decode(sourcePath, sourcePageIndex, dpi);

        return A4PreviewRenderer.RenderPng(
            job,
            layout,
            source,
            page: outputPageIndex,
            dpi: dpi);
    }

    private static SKBitmap Decode(
        string sourcePath,
        int sourcePageIndex,
        int dpi)
    {
        var extension = Path.GetExtension(sourcePath);

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return PdfPageRasterizer.RenderPage(sourcePath, sourcePageIndex, dpi);

        if (sourcePageIndex != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePageIndex),
                "Raster image sources only have source page index 0.");
        }

        return SKBitmap.Decode(sourcePath)
            ?? throw new InvalidDataException("The raster source could not be decoded.");
    }
}
