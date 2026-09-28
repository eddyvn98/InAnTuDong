using PrintAI.Domain;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class SourceCropCalculator
{
    public static SKRect Calculate(
        SKBitmap source,
        SourceSpec sourceSpec,
        float targetWidthPx,
        float targetHeightPx,
        SourceCropSpec crop)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceSpec);
        ArgumentNullException.ThrowIfNull(crop);

        return crop.Mode switch
        {
            SourceCropMode.AutoTrimWhite =>
                AutoTrimWhite(source, crop.WhiteThreshold),

            SourceCropMode.CenterToTargetAspect =>
                CenterToAspect(
                    source.Width,
                    source.Height,
                    targetWidthPx,
                    targetHeightPx),

            SourceCropMode.EdgesMm =>
                FromPhysicalEdges(
                    source,
                    sourceSpec,
                    crop.EdgesMm),

            _ => throw new ArgumentOutOfRangeException(
                nameof(crop.Mode))
        };
    }

    private static SKRect AutoTrimWhite(
        SKBitmap source,
        byte threshold)
    {
        var stride = Math.Max(
            1,
            (int)Math.Ceiling(
                Math.Max(source.Width, source.Height) / 1024d));

        var minX = source.Width;
        var minY = source.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < source.Height; y += stride)
        {
            for (var x = 0; x < source.Width; x += stride)
            {
                var pixel = source.GetPixel(x, y);
                if (!IsContent(pixel, threshold))
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < 0 || maxY < 0)
            return new SKRect(0, 0, source.Width, source.Height);

        var left = Math.Max(0, minX - stride);
        var top = Math.Max(0, minY - stride);
        var right = Math.Min(source.Width, maxX + stride + 1);
        var bottom = Math.Min(source.Height, maxY + stride + 1);

        return new SKRect(left, top, right, bottom);
    }

    private static bool IsContent(
        SKColor pixel,
        byte threshold)
    {
        if (pixel.Alpha == 0)
            return false;

        return pixel.Red < threshold ||
               pixel.Green < threshold ||
               pixel.Blue < threshold;
    }

    private static SKRect CenterToAspect(
        int sourceWidth,
        int sourceHeight,
        float targetWidth,
        float targetHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetWidth));

        var sourceAspect = sourceWidth / (double)sourceHeight;
        var targetAspect = targetWidth / (double)targetHeight;

        if (Math.Abs(sourceAspect - targetAspect) < 0.000001)
            return new SKRect(0, 0, sourceWidth, sourceHeight);

        if (sourceAspect > targetAspect)
        {
            var width = sourceHeight * targetAspect;
            var left = (sourceWidth - width) / 2;
            return new SKRect(
                (float)left,
                0,
                (float)(left + width),
                sourceHeight);
        }

        var height = sourceWidth / targetAspect;
        var top = (sourceHeight - height) / 2;
        return new SKRect(
            0,
            (float)top,
            sourceWidth,
            (float)(top + height));
    }

    private static SKRect FromPhysicalEdges(
        SKBitmap source,
        SourceSpec sourceSpec,
        CropEdgesSpec? edges)
    {
        if (edges is null)
            throw new ArgumentException(
                "EdgesMm crop requires edge values.",
                nameof(edges));

        if (sourceSpec.OriginalWidthMm is not double widthMm ||
            sourceSpec.OriginalHeightMm is not double heightMm ||
            widthMm <= 0 ||
            heightMm <= 0)
        {
            throw new ArgumentException(
                "Millimetre crop requires trusted source physical dimensions.",
                nameof(sourceSpec));
        }

        var left = edges.LeftMm / widthMm * source.Width;
        var right = source.Width -
            (edges.RightMm / widthMm * source.Width);
        var top = edges.TopMm / heightMm * source.Height;
        var bottom = source.Height -
            (edges.BottomMm / heightMm * source.Height);

        if (right <= left || bottom <= top)
            throw new ArgumentException(
                "Crop edges leave no source content.",
                nameof(edges));

        return new SKRect(
            (float)left,
            (float)top,
            (float)right,
            (float)bottom);
    }
}
