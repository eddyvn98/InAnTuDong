using PrintAI.Domain;
using PrintAI.ImageDecoding;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class SourceJobRenderer
{
    public static byte[] RenderSourceThumbnailPng(
        string sourcePath,
        int sourcePageIndex,
        int maxDimension = 768)
    {
        if (maxDimension is < 128 or > 2048)
            throw new ArgumentOutOfRangeException(nameof(maxDimension));

        using var source = Decode(
            sourcePath,
            sourcePageIndex,
            dpi: 96);

        var scale = Math.Min(
            1d,
            (double)maxDimension / Math.Max(source.Width, source.Height));

        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var sampling = new SKSamplingOptions(
            SKFilterMode.Linear,
            SKMipmapMode.Linear);

        canvas.DrawBitmap(
            source,
            new SKRect(0, 0, source.Width, source.Height),
            new SKRect(0, 0, width, height),
            sampling);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    public static int GetOutputPageCount(PrintJobSpec job)
    {
        var layout = LayoutEngine.Layout(job);
        return layout.Placements.Count == 0
            ? 0
            : layout.Placements.Max(p => p.Page) + 1;
    }

    public static byte[] RenderMixedA4(
        PrintJobSpec job,
        int outputPageIndex,
        int dpi)
    {
        var layout = LayoutEngine.Layout(job);
        var pageCount = layout.Placements.Count == 0
            ? 0
            : layout.Placements.Max(p => p.Page) + 1;

        if (outputPageIndex < 0 || outputPageIndex >= pageCount)
            throw new ArgumentOutOfRangeException(nameof(outputPageIndex));

        var sources = new List<SKBitmap>(job.Sources.Count);

        try
        {
            foreach (var source in job.Sources)
            {
                sources.Add(source.IsBlank
                    ? CreateBlank()
                    : Decode(
                        source.Path,
                        source.PageIndex,
                        dpi));
            }

            return A4PreviewRenderer.RenderPng(
                job,
                layout,
                sources,
                page: outputPageIndex,
                dpi: dpi);
        }
        finally
        {
            foreach (var source in sources)
                source.Dispose();
        }
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

    private static SKBitmap CreateBlank()
    {
        var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(SKColors.White);
        return bitmap;
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

        if (extension.Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))
        {
            var png = HeicDecoder.DecodeToPng(sourcePath);
            return SKBitmap.Decode(png)
                ?? throw new InvalidDataException("The decoded HEIC image could not be rendered.");
        }

        return SKBitmap.Decode(sourcePath)
            ?? throw new InvalidDataException("The raster source could not be decoded.");
    }
}
