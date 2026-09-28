using PrintAI.Domain;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

internal static class PosterTileGuidesRenderer
{
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
}
