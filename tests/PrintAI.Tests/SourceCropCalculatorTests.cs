using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class SourceCropCalculatorTests
{
    [Fact]
    public void AutoTrimWhite_FindsContentBounds()
    {
        using var bitmap = new SKBitmap(100, 100);
        bitmap.Erase(SKColors.White);

        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Black })
        {
            canvas.DrawRect(
                new SKRect(20, 30, 80, 70),
                paint);
        }

        var crop = SourceCropCalculator.Calculate(
            bitmap,
            new SourceSpec("image.png"),
            targetWidthPx: 100,
            targetHeightPx: 100,
            new SourceCropSpec(
                SourceCropMode.AutoTrimWhite));

        Assert.InRange(crop.Left, 18, 20);
        Assert.InRange(crop.Top, 28, 30);
        Assert.InRange(crop.Right, 80, 82);
        Assert.InRange(crop.Bottom, 70, 72);
    }

    [Fact]
    public void AutoTrimWhite_AllWhiteFallsBackToFullSource()
    {
        using var bitmap = new SKBitmap(80, 60);
        bitmap.Erase(SKColors.White);

        var crop = SourceCropCalculator.Calculate(
            bitmap,
            new SourceSpec("image.png"),
            100,
            100,
            new SourceCropSpec(
                SourceCropMode.AutoTrimWhite));

        Assert.Equal(new SKRect(0, 0, 80, 60), crop);
    }

    [Fact]
    public void CenterToTargetAspect_CropsWideImageFromBothSides()
    {
        using var bitmap = new SKBitmap(200, 100);

        var crop = SourceCropCalculator.Calculate(
            bitmap,
            new SourceSpec("image.png"),
            targetWidthPx: 100,
            targetHeightPx: 100,
            new SourceCropSpec(
                SourceCropMode.CenterToTargetAspect));

        Assert.Equal(50, crop.Left, 5);
        Assert.Equal(150, crop.Right, 5);
        Assert.Equal(0, crop.Top, 5);
        Assert.Equal(100, crop.Bottom, 5);
    }

    [Fact]
    public void EdgesMm_MapsPhysicalTopCropToPixels()
    {
        using var bitmap = new SKBitmap(100, 200);

        var crop = SourceCropCalculator.Calculate(
            bitmap,
            new SourceSpec(
                "doc.pdf",
                OriginalWidthMm: 100,
                OriginalHeightMm: 200),
            targetWidthPx: 100,
            targetHeightPx: 200,
            new SourceCropSpec(
                SourceCropMode.EdgesMm,
                new CropEdgesSpec(TopMm: 10)));

        Assert.Equal(10, crop.Top, 5);
        Assert.Equal(200, crop.Bottom, 5);
    }
}
