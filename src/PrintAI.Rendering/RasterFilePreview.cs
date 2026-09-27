using PrintAI.Domain;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class RasterFilePreview
{
    public static byte[] RenderA4(
        PrintJobSpec job,
        string sourcePath,
        int dpi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        using var bitmap = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidDataException("The raster source could not be decoded.");

        var layout = LayoutEngine.Layout(job);
        return A4PreviewRenderer.RenderPng(job, layout, bitmap, page: 0, dpi: dpi);
    }
}
