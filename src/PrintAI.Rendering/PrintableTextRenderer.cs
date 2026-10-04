using SkiaSharp;

namespace PrintAI.Rendering;

public static class PrintableTextRenderer
{
    private const double A4WidthMm = 210;
    private const double A4HeightMm = 297;
    private const double MarginMm = 15;

    public static byte[] RenderA4Png(
        string content,
        int dpi = 180)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Printable text content is required.", nameof(content));
        if (dpi is < 72 or > 300)
            throw new ArgumentOutOfRangeException(nameof(dpi));

        var width = MmToPx(A4WidthMm, dpi);
        var height = MmToPx(A4HeightMm, dpi);
        var margin = MmToPx(MarginMm, dpi);
        var maxWidth = width - (margin * 2);
        var maxHeight = height - (margin * 2);

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var typeface =
            SKTypeface.FromFamilyName("Arial") ??
            SKTypeface.FromFamilyName("Helvetica") ??
            SKTypeface.FromFamilyName("Noto Sans") ??
            SKTypeface.Default;
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = typeface,
            TextSize = Math.Max(18, MmToPx(4.2, dpi))
        };

        var lineHeight = paint.TextSize * 1.45f;
        var maxLines = Math.Max(1, (int)Math.Floor(maxHeight / lineHeight));
        var lines = Wrap(content.Replace("\r\n", "\n"), paint, maxWidth, maxLines);

        var y = margin + paint.TextSize;
        foreach (var line in lines)
        {
            canvas.DrawText(line, margin, y, paint);
            y += lineHeight;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static IReadOnlyList<string> Wrap(
        string content,
        SKPaint paint,
        float maxWidth,
        int maxLines)
    {
        var result = new List<string>(maxLines);
        foreach (var paragraph in content.Split('\n'))
        {
            if (result.Count >= maxLines)
                break;

            if (string.IsNullOrWhiteSpace(paragraph))
            {
                result.Add(string.Empty);
                continue;
            }

            var line = string.Empty;
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (paint.MeasureText(candidate) <= maxWidth)
                {
                    line = candidate;
                    continue;
                }

                if (line.Length > 0)
                {
                    result.Add(line);
                    if (result.Count >= maxLines)
                        break;
                }

                line = FitLongWord(word, paint, maxWidth);
            }

            if (result.Count < maxLines && line.Length > 0)
                result.Add(line);
        }

        if (result.Count == maxLines && paint.MeasureText(result[^1] + "…") <= maxWidth)
            result[^1] += "…";

        return result;
    }

    private static string FitLongWord(
        string word,
        SKPaint paint,
        float maxWidth)
    {
        if (paint.MeasureText(word) <= maxWidth)
            return word;

        var length = word.Length;
        while (length > 1 && paint.MeasureText(word[..length] + "…") > maxWidth)
            length--;

        return word[..Math.Max(1, length)] + "…";
    }

    private static int MmToPx(
        double mm,
        int dpi) =>
        Math.Max(1, (int)Math.Round(mm / 25.4 * dpi));
}
