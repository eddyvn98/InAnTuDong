using PrintAI.Domain;
using PrintAI.Layout;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class A4PreviewRenderer
{
    public static byte[] RenderPng(
        PrintJobSpec job,
        LayoutResult layout,
        SKBitmap source,
        int page = 0,
        int dpi = 96)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi));

        var (paperWidthMm, paperHeightMm) = GetPaperSize(job.Paper);
        var widthPx = MmToPx(paperWidthMm, dpi);
        var heightPx = MmToPx(paperHeightMm, dpi);

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        foreach (var placement in layout.Placements.Where(p => p.Page == page))
        {
            DrawPlacement(canvas, source, placement, job.Layout.Fit, dpi);

            if (job.Layout.CutMarks)
                DrawCutMarks(canvas, placement, dpi);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawPlacement(
        SKCanvas canvas,
        SKBitmap source,
        Placement placement,
        FitMode fit,
        int dpi)
    {
        var left = MmToPxF(placement.XMm, dpi);
        var top = MmToPxF(placement.YMm, dpi);
        var width = MmToPxF(placement.WidthMm, dpi);
        var height = MmToPxF(placement.HeightMm, dpi);

        canvas.Save();

        if (placement.Rotated)
        {
            var centerX = left + (width / 2);
            var centerY = top + (height / 2);
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees(90);
            left = -height / 2;
            top = -width / 2;
            (width, height) = (height, width);
        }

        var target = new SKRect(left, top, left + width, top + height);
        var geometry = ContentFitCalculator.Calculate(
            source.Width,
            source.Height,
            target.Width,
            target.Height,
            fit);

        var sourceRect = ToSourceRect(source, geometry.Source);
        var destinationRect = ToDestinationRect(target, geometry.Destination);

        canvas.ClipRect(target);
        canvas.DrawBitmap(source, sourceRect, destinationRect);
        canvas.Restore();
    }

    private static void DrawCutMarks(SKCanvas canvas, Placement placement, int dpi)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = Math.Max(1, MmToPxF(0.2, dpi)),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        foreach (var mark in CutMarkGenerator.Create(placement))
        {
            canvas.DrawLine(
                MmToPxF(mark.X1Mm, dpi),
                MmToPxF(mark.Y1Mm, dpi),
                MmToPxF(mark.X2Mm, dpi),
                MmToPxF(mark.Y2Mm, dpi),
                paint);
        }
    }

    private static SKRect ToSourceRect(SKBitmap source, NormalizedRect rect) =>
        new(
            (float)(rect.X * source.Width),
            (float)(rect.Y * source.Height),
            (float)((rect.X + rect.Width) * source.Width),
            (float)((rect.Y + rect.Height) * source.Height));

    private static SKRect ToDestinationRect(SKRect target, NormalizedRect rect) =>
        new(
            target.Left + (float)(rect.X * target.Width),
            target.Top + (float)(rect.Y * target.Height),
            target.Left + (float)((rect.X + rect.Width) * target.Width),
            target.Top + (float)((rect.Y + rect.Height) * target.Height));

    private static (double Width, double Height) GetPaperSize(PaperSpec paper) =>
        paper.Orientation == PageOrientation.Portrait
            ? (paper.WidthMm, paper.HeightMm)
            : (paper.HeightMm, paper.WidthMm);

    private static int MmToPx(double mm, int dpi) =>
        Math.Max(1, (int)Math.Round(mm / 25.4 * dpi));

    private static float MmToPxF(double mm, int dpi) =>
        (float)(mm / 25.4 * dpi);
}
