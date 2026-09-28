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

        if (job.Layout.Mode == LayoutMode.PosterTile)
        {
            var placement = layout.Placements
                .Single(item =>
                    item.Page == outputPageIndex);
            var sourceSpec =
                job.Sources[placement.SourceIndex];

            using var source = Decode(
                sourceSpec.Path,
                sourceSpec.PageIndex,
                dpi);

            var tileJob = job with
            {
                Sources = [sourceSpec]
            };

            var tileLayout = layout with
            {
                Placements =
                [
                    placement with
                    {
                        Index = 0,
                        Page = 0,
                        SourceIndex = 0
                    }
                ]
            };

            return A4PreviewRenderer.RenderPng(
                tileJob,
                tileLayout,
                source,
                page: 0,
                dpi: dpi);
        }

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
            return DecodeOriented(png);
        }

        return DecodeOriented(File.ReadAllBytes(sourcePath));
    }

    private static SKBitmap DecodeOriented(byte[] encoded)
    {
        using var stream = new SKMemoryStream(encoded);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException("The raster source could not be decoded.");

        var info = codec.Info;
        var swapsAxes = codec.EncodedOrigin is
            SKEncodedOrigin.LeftTop or
            SKEncodedOrigin.RightTop or
            SKEncodedOrigin.RightBottom or
            SKEncodedOrigin.LeftBottom;

        var output = new SKBitmap(
            swapsAxes ? info.Height : info.Width,
            swapsAxes ? info.Width : info.Height);

        using var decoded = new SKBitmap(info.Width, info.Height);
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());

        if (result is not SKCodecResult.Success and
            not SKCodecResult.IncompleteInput)
        {
            output.Dispose();
            throw new InvalidDataException(
                $"The raster source could not be decoded ({result}).");
        }

        using var canvas = new SKCanvas(output);
        ApplyEncodedOrigin(
            canvas,
            codec.EncodedOrigin,
            decoded.Width,
            decoded.Height);
        var sampling = new SKSamplingOptions(
            SKFilterMode.Nearest,
            SKMipmapMode.None);
        canvas.DrawBitmap(
            decoded,
            new SKRect(0, 0, decoded.Width, decoded.Height),
            new SKRect(0, 0, decoded.Width, decoded.Height),
            sampling);
        canvas.Flush();
        return output;
    }

    private static void ApplyEncodedOrigin(
        SKCanvas canvas,
        SKEncodedOrigin origin,
        int width,
        int height)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(height, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(height, width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, width);
                canvas.RotateDegrees(-90);
                break;
        }
    }
}
