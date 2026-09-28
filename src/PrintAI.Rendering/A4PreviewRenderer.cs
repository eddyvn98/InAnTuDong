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

            var sourceSpec =
                job.Sources[placement.SourceIndex];

            if (sourceSpec.PosterTile is not null)
            {
                DrawPosterTile(
                    canvas,
                    sources[placement.SourceIndex],
                    sourceSpec,
                    placement,
                    dpi);

                if (sourceSpec.PosterTile.RegistrationMarks)
                {
                    DrawPosterRegistrationMarks(
                        canvas,
                        placement,
                        sourceSpec.PosterTile,
                        dpi);
                }

                if (sourceSpec.PosterTile.TileLabel)
                {
                    DrawPosterTileLabel(
                        canvas,
                        sourceSpec.PosterTile,
                        job.Layout.MarginMm,
                        dpi);
                }
            }
            else
            {
                DrawPlacement(
                    canvas,
                    sources[placement.SourceIndex],
                    sourceSpec,
                    placement,
                    canvasPlacement?.Fit ?? job.Layout.Fit,
                    job.Layout.PhysicalScale,
                    job.Layout.SourceCrop,
                    dpi,
                    canvasPlacement);

                if (job.Layout.ItemBorder)
                    DrawItemBorder(canvas, placement, dpi);

                if (job.Layout.CutMarks)
                    DrawCutMarks(canvas, placement, dpi);
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawPosterTile(
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
            MmToPxF(placement.XMm, dpi),
            MmToPxF(placement.YMm, dpi),
            MmToPxF(
                placement.XMm + placement.WidthMm,
                dpi),
            MmToPxF(
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

            DrawPosterBitmap(
                canvas,
                source,
                sourceRect,
                target);

            canvas.Restore();
            return;
        }

        var content = ContainContentRect(
            tile.TargetWidthMm,
            tile.TargetHeightMm,
            sourceAspect);

        var tileRect = new MmRect(
            tile.CanvasXmm,
            tile.CanvasYmm,
            tile.CanvasWidthMm,
            tile.CanvasHeightMm);

        var intersection = Intersect(
            content,
            tileRect);

        if (intersection is null)
        {
            canvas.Restore();
            return;
        }

        var visible = intersection.Value;

        var sourceRectContain = new SKRect(
            (float)(((visible.X - content.X) /
                content.Width) * source.Width),
            (float)(((visible.Y - content.Y) /
                content.Height) * source.Height),
            (float)((((visible.X - content.X) +
                visible.Width) /
                content.Width) * source.Width),
            (float)((((visible.Y - content.Y) +
                visible.Height) /
                content.Height) * source.Height));

        var destination = new SKRect(
            target.Left +
                MmToPxF(
                    visible.X - tile.CanvasXmm,
                    dpi),
            target.Top +
                MmToPxF(
                    visible.Y - tile.CanvasYmm,
                    dpi),
            target.Left +
                MmToPxF(
                    visible.X - tile.CanvasXmm +
                    visible.Width,
                    dpi),
            target.Top +
                MmToPxF(
                    visible.Y - tile.CanvasYmm +
                    visible.Height,
                    dpi));

        DrawPosterBitmap(
            canvas,
            source,
            sourceRectContain,
            destination);

        canvas.Restore();
    }

    private static void DrawPosterBitmap(
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

    private static NormalizedRect CoverCrop(
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

    private static MmRect ContainContentRect(
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

        if (x2 <= x1 || y2 <= y1)
            return null;

        return new(
            x1,
            y1,
            x2 - x1,
            y2 - y1);
    }

    private static void DrawPosterRegistrationMarks(
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
                MmToPxF(0.25, dpi)),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        var size = Math.Min(
            2.5,
            Math.Max(
                1.5,
                tile.OverlapMm / 3));

        var x1 = placement.XMm;
        var x2 =
            placement.XMm + placement.WidthMm;
        var y1 = placement.YMm;
        var y2 =
            placement.YMm + placement.HeightMm;
        var midX = (x1 + x2) / 2;
        var midY = (y1 + y2) / 2;

        if (tile.Column > 0)
        {
            DrawCross(
                canvas,
                x1 + size,
                midY,
                size,
                dpi,
                paint);
        }

        if (tile.Column < tile.Columns - 1)
        {
            DrawCross(
                canvas,
                x2 - size,
                midY,
                size,
                dpi,
                paint);
        }

        if (tile.Row > 0)
        {
            DrawCross(
                canvas,
                midX,
                y1 + size,
                size,
                dpi,
                paint);
        }

        if (tile.Row < tile.Rows - 1)
        {
            DrawCross(
                canvas,
                midX,
                y2 - size,
                size,
                dpi,
                paint);
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
        var x = MmToPxF(xMm, dpi);
        var y = MmToPxF(yMm, dpi);
        var half = MmToPxF(sizeMm / 2, dpi);

        canvas.DrawLine(
            x - half,
            y,
            x + half,
            y,
            paint);
        canvas.DrawLine(
            x,
            y - half,
            x,
            y + half,
            paint);
    }

    private static void DrawPosterTileLabel(
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
            $"R{tile.Row + 1}C{tile.Column + 1} · " +
            $"{number}/{tile.Rows * tile.Columns}";

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };
        using var font = new SKFont(
            SKTypeface.Default,
            Math.Max(
                8,
                MmToPxF(2.8, dpi)));

        var x = MmToPxF(1.5, dpi);
        var y = MmToPxF(
            Math.Max(
                3.2,
                marginMm - 0.8),
            dpi);

        canvas.DrawText(
            text,
            x,
            y,
            font,
            paint);
    }

    private readonly record struct MmRect(
        double X,
        double Y,
        double Width,
        double Height);

    private static void DrawPlacement(
        SKCanvas canvas,
        SKBitmap source,
        SourceSpec sourceSpec,
        Placement placement,
        FitMode fit,
        PhysicalScaleSpec? physicalScale,
        SourceCropSpec? sourceCrop,
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

        var cropRect = sourceCrop is null
            ? new SKRect(0, 0, source.Width, source.Height)
            : SourceCropCalculator.Calculate(
                source,
                sourceSpec,
                target.Width,
                target.Height,
                sourceCrop);

        var geometry = physicalScale is null
            ? ContentFitCalculator.Calculate(
                cropRect.Width,
                cropRect.Height,
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
            ToSourceRect(cropRect, geometry.Source),
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

    private static SKRect ToSourceRect(
        SKRect sourceRect,
        NormalizedRect rect) =>
        new(
            sourceRect.Left + (float)(rect.X * sourceRect.Width),
            sourceRect.Top + (float)(rect.Y * sourceRect.Height),
            sourceRect.Left +
                (float)((rect.X + rect.Width) * sourceRect.Width),
            sourceRect.Top +
                (float)((rect.Y + rect.Height) * sourceRect.Height));

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
