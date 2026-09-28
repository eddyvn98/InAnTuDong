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
        int dpi = 96) =>
        RenderPng(job, layout, [source], page, dpi);

    public static byte[] RenderPng(
        PrintJobSpec job,
        LayoutResult layout,
        IReadOnlyList<SKBitmap> sources,
        int page = 0,
        int dpi = 96)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
            throw new ArgumentException("At least one rendered source is required.", nameof(sources));

        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi));

        var (paperWidthMm, paperHeightMm) = GetPaperSize(job.Paper);
        var widthPx = MmToPx(paperWidthMm, dpi);
        var heightPx = MmToPx(paperHeightMm, dpi);

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var pagePlacements = layout.Placements.Where(p => p.Page == page);
        if (job.Layout.Mode == LayoutMode.Canvas && job.Layout.Canvas is not null)
        {
            pagePlacements = pagePlacements.OrderBy(p =>
                job.Layout.Canvas.Placements[p.Index].ZIndex);
        }

        foreach (var placement in pagePlacements)
        {
            if (placement.SourceIndex < 0 ||
                placement.SourceIndex >= sources.Count)
            {
                throw new InvalidOperationException(
                    $"Placement references unavailable source index {placement.SourceIndex}.");
            }

            var canvasPlacement =
                job.Layout.Mode == LayoutMode.Canvas && job.Layout.Canvas is not null
                    ? job.Layout.Canvas.Placements[placement.Index]
                    : null;

            DrawPlacement(
                canvas,
                sources[placement.SourceIndex],
                job.Sources[placement.SourceIndex],
                placement,
                canvasPlacement?.Fit ?? job.Layout.Fit,
                job.Layout.PhysicalScale,
                dpi,
                canvasPlacement);

            if (job.Layout.ItemBorder)
                DrawItemBorder(canvas, placement, dpi);

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
        SourceSpec sourceSpec,
        Placement placement,
        FitMode fit,
        PhysicalScaleSpec? physicalScale,
        int dpi,
        CanvasPlacementSpec? canvasPlacement = null)
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

        if (canvasPlacement is not null && Math.Abs(canvasPlacement.RotationDegrees) > 0.01)
        {
            var centerX = target.MidX;
            var centerY = target.MidY;
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees((float)canvasPlacement.RotationDegrees);
            target = new SKRect(-width / 2, -height / 2, width / 2, height / 2);
        }

        ClipFrame(canvas, target, canvasPlacement?.Shape, dpi);

        var targetWidthMm = placement.Rotated
            ? placement.HeightMm
            : placement.WidthMm;
        var targetHeightMm = placement.Rotated
            ? placement.WidthMm
            : placement.HeightMm;

        var geometry = physicalScale is null
            ? ContentFitCalculator.Calculate(
                source.Width,
                source.Height,
                target.Width,
                target.Height,
                fit)
            : PhysicalScaleCalculator.Calculate(
                source.Width,
                source.Height,
                target.Width,
                target.Height,
                targetWidthMm,
                targetHeightMm,
                sourceSpec.OriginalWidthMm,
                sourceSpec.OriginalHeightMm,
                physicalScale);

        var transform = canvasPlacement?.Transform ?? new ImageTransformSpec();
        var sourceRect = TransformSourceRect(
            ToSourceRect(source, geometry.Source),
            source,
            transform);
        var destinationRect = ToDestinationRect(target, geometry.Destination);

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        canvas.DrawBitmap(source, sourceRect, destinationRect, sampling);
        canvas.Restore();
    }

    private static void DrawItemBorder(
        SKCanvas canvas,
        Placement placement,
        int dpi)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = Math.Max(1, MmToPxF(0.2, dpi)),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawRect(
            MmToPxF(placement.XMm, dpi),
            MmToPxF(placement.YMm, dpi),
            MmToPxF(placement.WidthMm, dpi),
            MmToPxF(placement.HeightMm, dpi),
            paint);
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

    private static void ClipFrame(
        SKCanvas canvas,
        SKRect target,
        ShapeSpec? shape,
        int dpi)
    {
        shape ??= new ShapeSpec();

        switch (shape.Kind)
        {
            case FrameShape.Circle:
            {
                var radius = Math.Min(target.Width, target.Height) / 2;
                using var circle = new SKRoundRect(
                    new SKRect(
                        target.MidX - radius,
                        target.MidY - radius,
                        target.MidX + radius,
                        target.MidY + radius),
                    radius,
                    radius);
                canvas.ClipRoundRect(circle);
                break;
            }
            case FrameShape.Ellipse:
            {
                using var ellipse = new SKRoundRect(
                    target,
                    target.Width / 2,
                    target.Height / 2);
                canvas.ClipRoundRect(ellipse);
                break;
            }
            case FrameShape.RoundedRectangle:
            {
                var radius = Math.Min(
                    MmToPxF(shape.CornerRadiusMm, dpi),
                    Math.Min(target.Width, target.Height) / 2);
                using var rounded = new SKRoundRect(target, radius, radius);
                canvas.ClipRoundRect(rounded);
                break;
            }
            default:
                canvas.ClipRect(target);
                break;
        }
    }


    private static SKRect TransformSourceRect(
        SKRect sourceRect,
        SKBitmap source,
        ImageTransformSpec transform)
    {
        var scale = Math.Max(0.01, transform.Scale);
        var width = sourceRect.Width / (float)scale;
        var height = sourceRect.Height / (float)scale;
        var maxLeft = Math.Max(0, source.Width - width);
        var maxTop = Math.Max(0, source.Height - height);
        var centerX = sourceRect.MidX +
            (float)(transform.OffsetX * sourceRect.Width * 0.5);
        var centerY = sourceRect.MidY +
            (float)(transform.OffsetY * sourceRect.Height * 0.5);
        var left = Math.Clamp(centerX - width / 2, 0, maxLeft);
        var top = Math.Clamp(centerY - height / 2, 0, maxTop);

        return new SKRect(left, top, left + width, top + height);
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
