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

    public static void DrawRegistrationMarks(
        SKCanvas canvas,
        Placement placement,
        PosterTileSourceSpec tile,
        int dpi)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = Math.Max(
                1,
                MmToPx(0.25, dpi)),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        var size = Math.Min(
            2.5,
            Math.Max(
                1.5,
                tile.OverlapMm / 3));

        var left = placement.XMm;
        var right =
            placement.XMm + placement.WidthMm;
        var top = placement.YMm;
        var bottom =
            placement.YMm + placement.HeightMm;
        var midX = (left + right) / 2;
        var midY = (top + bottom) / 2;

        if (tile.Column > 0)
            DrawCross(canvas, left + size, midY, size, dpi, paint);

        if (tile.Column < tile.Columns - 1)
            DrawCross(canvas, right - size, midY, size, dpi, paint);

        if (tile.Row > 0)
            DrawCross(canvas, midX, top + size, size, dpi, paint);

        if (tile.Row < tile.Rows - 1)
            DrawCross(canvas, midX, bottom - size, size, dpi, paint);
    }

    public static void DrawTileLabel(
        SKCanvas canvas,
        PosterTileSourceSpec tile,
        double marginMm,
        int dpi)
    {
        var number =
            (tile.Row * tile.Columns) +
            tile.Column +
            1;
        var text =
            $"{tile.Row + 1}-{tile.Column + 1} " +
            $"{number}/{tile.Rows * tile.Columns}";

        const double cellMm = 0.45;
        const double glyphGapMm = 0.35;
        var glyphWidthMm = 3 * cellMm;
        var widthMm =
            (text.Length * glyphWidthMm) +
            (Math.Max(0, text.Length - 1) * glyphGapMm);
        var heightMm = 5 * cellMm;
        var xMm = 1.2;
        var yMm = Math.Max(
            0.7,
            Math.Min(
                Math.Max(0.7, marginMm - heightMm - 0.5),
                2.0));

        using var background = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill
        };
        using var ink = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = false
        };

        canvas.DrawRect(
            MmToPx(xMm - 0.4, dpi),
            MmToPx(yMm - 0.3, dpi),
            MmToPx(widthMm + 0.8, dpi),
            MmToPx(heightMm + 0.6, dpi),
            background);

        var cursor = xMm;
        foreach (var character in text)
        {
            DrawGlyph(
                canvas,
                character,
                cursor,
                yMm,
                cellMm,
                dpi,
                ink);

            cursor += glyphWidthMm + glyphGapMm;
        }
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

    private static void DrawCross(
        SKCanvas canvas,
        double xMm,
        double yMm,
        double sizeMm,
        int dpi,
        SKPaint paint)
    {
        var x = MmToPx(xMm, dpi);
        var y = MmToPx(yMm, dpi);
        var half = MmToPx(sizeMm / 2, dpi);

        canvas.DrawLine(x - half, y, x + half, y, paint);
        canvas.DrawLine(x, y - half, x, y + half, paint);
    }

    private static void DrawGlyph(
        SKCanvas canvas,
        char character,
        double xMm,
        double yMm,
        double cellMm,
        int dpi,
        SKPaint paint)
    {
        var pattern = Glyph(character);
        if (pattern is null)
            return;

        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (pattern[(row * 3) + column] != '1')
                    continue;

                canvas.DrawRect(
                    MmToPx(xMm + column * cellMm, dpi),
                    MmToPx(yMm + row * cellMm, dpi),
                    Math.Max(1, MmToPx(cellMm, dpi)),
                    Math.Max(1, MmToPx(cellMm, dpi)),
                    paint);
            }
        }
    }

    private static string? Glyph(char value) =>
        value switch
        {
            '0' => "111101101101111",
            '1' => "010110010010111",
            '2' => "111001111100111",
            '3' => "111001111001111",
            '4' => "101101111001001",
            '5' => "111100111001111",
            '6' => "111100111101111",
            '7' => "111001001001001",
            '8' => "111101111101111",
            '9' => "111101111001111",
            '-' => "000000111000000",
            '/' => "001001010100100",
            ' ' => "000000000000000",
            _ => null
        };

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
