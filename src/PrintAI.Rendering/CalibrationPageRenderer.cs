using SkiaSharp;

namespace PrintAI.Rendering;

public static class CalibrationPageRenderer
{
    public const double A4WidthMm = 210;
    public const double A4HeightMm = 297;

    public static byte[] RenderA4Png(int dpi = 300)
    {
        if (dpi is < 72 or > 600)
            throw new ArgumentOutOfRangeException(nameof(dpi), "Calibration DPI must be between 72 and 600.");

        var widthPx = MmToPx(A4WidthMm, dpi);
        var heightPx = MmToPx(A4HeightMm, dpi);

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var major = Stroke(dpi, 0.30f);
        using var minor = Stroke(dpi, 0.15f);

        DrawRectMm(canvas, major, 10, 10, 190, 277, dpi);
        DrawRectMm(canvas, major, 20, 30, 100, 100, dpi);
        DrawRectMm(canvas, major, 140, 30, 50, 50, dpi);

        DrawHorizontalRuler(canvas, major, minor, 20, 160, 100, dpi);
        DrawVerticalRuler(canvas, major, minor, 20, 175, 100, dpi);

        DrawCross(canvas, major, 105, 148.5, 10, dpi);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawHorizontalRuler(
        SKCanvas canvas,
        SKPaint major,
        SKPaint minor,
        double xMm,
        double yMm,
        int lengthMm,
        int dpi)
    {
        canvas.DrawLine(Px(xMm, dpi), Px(yMm, dpi), Px(xMm + lengthMm, dpi), Px(yMm, dpi), major);

        for (var mm = 0; mm <= lengthMm; mm++)
        {
            var tick = mm % 10 == 0 ? 5 : mm % 5 == 0 ? 3 : 2;
            var paint = mm % 10 == 0 ? major : minor;
            canvas.DrawLine(
                Px(xMm + mm, dpi),
                Px(yMm, dpi),
                Px(xMm + mm, dpi),
                Px(yMm + tick, dpi),
                paint);
        }
    }

    private static void DrawVerticalRuler(
        SKCanvas canvas,
        SKPaint major,
        SKPaint minor,
        double xMm,
        double yMm,
        int lengthMm,
        int dpi)
    {
        canvas.DrawLine(Px(xMm, dpi), Px(yMm, dpi), Px(xMm, dpi), Px(yMm + lengthMm, dpi), major);

        for (var mm = 0; mm <= lengthMm; mm++)
        {
            var tick = mm % 10 == 0 ? 5 : mm % 5 == 0 ? 3 : 2;
            var paint = mm % 10 == 0 ? major : minor;
            canvas.DrawLine(
                Px(xMm, dpi),
                Px(yMm + mm, dpi),
                Px(xMm + tick, dpi),
                Px(yMm + mm, dpi),
                paint);
        }
    }

    private static void DrawCross(
        SKCanvas canvas,
        SKPaint paint,
        double centerXMm,
        double centerYMm,
        double sizeMm,
        int dpi)
    {
        var half = sizeMm / 2;
        canvas.DrawLine(
            Px(centerXMm - half, dpi),
            Px(centerYMm, dpi),
            Px(centerXMm + half, dpi),
            Px(centerYMm, dpi),
            paint);
        canvas.DrawLine(
            Px(centerXMm, dpi),
            Px(centerYMm - half, dpi),
            Px(centerXMm, dpi),
            Px(centerYMm + half, dpi),
            paint);
    }

    private static void DrawRectMm(
        SKCanvas canvas,
        SKPaint paint,
        double xMm,
        double yMm,
        double widthMm,
        double heightMm,
        int dpi) =>
        canvas.DrawRect(
            Px(xMm, dpi),
            Px(yMm, dpi),
            Px(widthMm, dpi),
            Px(heightMm, dpi),
            paint);

    private static SKPaint Stroke(int dpi, float widthMm) =>
        new()
        {
            Color = SKColors.Black,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, Px(widthMm, dpi))
        };

    private static int MmToPx(double mm, int dpi) =>
        Math.Max(1, (int)Math.Round(mm / 25.4 * dpi));

    private static float Px(double mm, int dpi) =>
        (float)(mm / 25.4 * dpi);
}
