using PrintAI.Domain;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

internal static class PosterTileRenderer
{
    public static void Draw(
        SKCanvas canvas,
        SKBitmap source,
        SourceSpec sourceSpec,
        Placement placement,
        int dpi)
    {
        var tile = sourceSpec.PosterTile
            ?? throw new ArgumentException(
                "Poster tile metadata is required.",
                nameof(sourceSpec));

        var target = new SKRect(
            MmToPx(placement.XMm, dpi),
            MmToPx(placement.YMm, dpi),
            MmToPx(
                placement.XMm + placement.WidthMm,
                dpi),
            MmToPx(
                placement.YMm + placement.HeightMm,
                dpi));

        var sourceAspect =
            source.Width / (double)source.Height;
        var posterAspect =
            tile.TargetWidthMm / tile.TargetHeightMm;

        canvas.Save();
        canvas.ClipRect(target);

        if (tile.Fit == FitMode.Cover)
        {
            DrawCover(
                canvas,
                source,
                tile,
                target,
                sourceAspect,
                posterAspect);

            canvas.Restore();
            return;
        }

        DrawContain(
            canvas,
            source,
            tile,
            target,
            sourceAspect,
            dpi);

        canvas.Restore();
    }

    private static void DrawCover(
        SKCanvas canvas,
        SKBitmap source,
        PosterTileSourceSpec tile,
        SKRect target,
        double sourceAspect,
        double posterAspect)
    {
        var crop = CoverCrop(
            sourceAspect,
            posterAspect);

        var tileX =
            tile.CanvasXmm / tile.TargetWidthMm;
        var tileY =
            tile.CanvasYmm / tile.TargetHeightMm;
        var tileWidth =
            tile.CanvasWidthMm / tile.TargetWidthMm;
        var tileHeight =
            tile.CanvasHeightMm / tile.TargetHeightMm;

        var sourceRect = new SKRect(
            (float)((crop.X + tileX * crop.Width) *
                source.Width),
            (float)((crop.Y + tileY * crop.Height) *
                source.Height),
            (float)((crop.X +
                (tileX + tileWidth) * crop.Width) *
                source.Width),
            (float)((crop.Y +
                (tileY + tileHeight) * crop.Height) *
                source.Height));

        DrawBitmap(
            canvas,
            source,
            sourceRect,
            target);
    }

    private static void DrawContain(
        SKCanvas canvas,
        SKBitmap source,
        PosterTileSourceSpec tile,
        SKRect target,
        double sourceAspect,
        int dpi)
    {
        var content = ContainRect(
            tile.TargetWidthMm,
            tile.TargetHeightMm,
            sourceAspect);
        var tileRect = new MmRect(
            tile.CanvasXmm,
            tile.CanvasYmm,
            tile.CanvasWidthMm,
            tile.CanvasHeightMm);
        var visible = Intersect(content, tileRect);

        if (visible is null)
            return;

        var area = visible.Value;
        var sourceRect = new SKRect(
            (float)(((area.X - content.X) /
                content.Width) * source.Width),
            (float)(((area.Y - content.Y) /
                content.Height) * source.Height),
            (float)((((area.X - content.X) +
                area.Width) /
                content.Width) * source.Width),
            (float)((((area.Y - content.Y) +
                area.Height) /
                content.Height) * source.Height));

        var destination = new SKRect(
            target.Left +
                MmToPx(area.X - tile.CanvasXmm, dpi),
            target.Top +
                MmToPx(area.Y - tile.CanvasYmm, dpi),
            target.Left +
                MmToPx(
                    area.X - tile.CanvasXmm + area.Width,
                    dpi),
            target.Top +
                MmToPx(
                    area.Y - tile.CanvasYmm + area.Height,
                    dpi));

        DrawBitmap(
            canvas,
            source,
            sourceRect,
            destination);
    }

    private static void DrawBitmap(
        SKCanvas canvas,
        SKBitmap source,
        SKRect sourceRect,
        SKRect destination)
    {
        var sampling = new SKSamplingOptions(
            SKFilterMode.Linear,
            SKMipmapMode.None);

        canvas.DrawBitmap(
            source,
            sourceRect,
            destination,
            sampling);
    }

    private static NormRect CoverCrop(
        double sourceAspect,
        double targetAspect)
    {
        if (sourceAspect > targetAspect)
        {
            var width = targetAspect / sourceAspect;
            return new(
                (1 - width) / 2,
                0,
                width,
                1);
        }

        var height = sourceAspect / targetAspect;
        return new(
            0,
            (1 - height) / 2,
            1,
            height);
    }

    private static MmRect ContainRect(
        double targetWidth,
        double targetHeight,
        double sourceAspect)
    {
        var targetAspect =
            targetWidth / targetHeight;

        if (sourceAspect > targetAspect)
        {
            var height =
                targetWidth / sourceAspect;

            return new(
                0,
                (targetHeight - height) / 2,
                targetWidth,
                height);
        }

        var width =
            targetHeight * sourceAspect;

        return new(
            (targetWidth - width) / 2,
            0,
            width,
            targetHeight);
    }

    private static MmRect? Intersect(
        MmRect left,
        MmRect right)
    {
        var x1 = Math.Max(left.X, right.X);
        var y1 = Math.Max(left.Y, right.Y);
        var x2 = Math.Min(
            left.X + left.Width,
            right.X + right.Width);
        var y2 = Math.Min(
            left.Y + left.Height,
            right.Y + right.Height);

        return x2 <= x1 || y2 <= y1
            ? null
            : new(
                x1,
                y1,
                x2 - x1,
                y2 - y1);
    }

    private static float MmToPx(
        double mm,
        int dpi) =>
        (float)(mm / 25.4 * dpi);

    private readonly record struct NormRect(
        double X,
        double Y,
        double Width,
        double Height);

    private readonly record struct MmRect(
        double X,
        double Y,
        double Width,
        double Height);
}
