using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class PhysicalScaleCalculatorTests
{
    [Fact]
    public void MaxFit_PreservesWholeSourceAndUsesLargestFit()
    {
        var result = PhysicalScaleCalculator.Calculate(
            sourcePixelWidth: 200,
            sourcePixelHeight: 100,
            targetWidthPx: 100,
            targetHeightPx: 100,
            targetWidthMm: 100,
            targetHeightMm: 100,
            sourceWidthMm: null,
            sourceHeightMm: null,
            scaling: new PhysicalScaleSpec(PhysicalScaleMode.MaxFit));

        Assert.Equal(new NormalizedRect(0, 0, 1, 1), result.Source);
        Assert.Equal(0, result.Destination.X, 6);
        Assert.Equal(0.25, result.Destination.Y, 6);
        Assert.Equal(1, result.Destination.Width, 6);
        Assert.Equal(0.5, result.Destination.Height, 6);
    }

    [Fact]
    public void ShrinkOnly_KeepsSmallerPhysicalPageAtOneHundredPercent()
    {
        var result = PhysicalScaleCalculator.Calculate(
            sourcePixelWidth: 1480,
            sourcePixelHeight: 2100,
            targetWidthPx: 2000,
            targetHeightPx: 2870,
            targetWidthMm: 200,
            targetHeightMm: 287,
            sourceWidthMm: 148,
            sourceHeightMm: 210,
            scaling: new PhysicalScaleSpec(PhysicalScaleMode.ShrinkOnly));

        Assert.Equal(148d / 200d, result.Destination.Width, 6);
        Assert.Equal(210d / 287d, result.Destination.Height, 6);
        Assert.True(result.Destination.Width < 1);
        Assert.True(result.Destination.Height < 1);
    }

    [Fact]
    public void ShrinkOnly_ReducesOversizePageButNeverUpscales()
    {
        var result = PhysicalScaleCalculator.Calculate(
            sourcePixelWidth: 2100,
            sourcePixelHeight: 2970,
            targetWidthPx: 2000,
            targetHeightPx: 2870,
            targetWidthMm: 200,
            targetHeightMm: 287,
            sourceWidthMm: 210,
            sourceHeightMm: 297,
            scaling: new PhysicalScaleSpec(PhysicalScaleMode.ShrinkOnly));

        Assert.Equal(1, result.Destination.Width, 6);
        Assert.True(result.Destination.Height < 1);
    }

    [Fact]
    public void Percent_EightyPercentUsesPhysicalSourceSize()
    {
        var result = PhysicalScaleCalculator.Calculate(
            sourcePixelWidth: 2100,
            sourcePixelHeight: 2970,
            targetWidthPx: 2000,
            targetHeightPx: 2870,
            targetWidthMm: 200,
            targetHeightMm: 287,
            sourceWidthMm: 210,
            sourceHeightMm: 297,
            scaling: new PhysicalScaleSpec(
                PhysicalScaleMode.Percent,
                Percent: 80));

        Assert.Equal(168d / 200d, result.Destination.Width, 6);
        Assert.Equal(237.6d / 287d, result.Destination.Height, 6);
    }

    [Fact]
    public void Percent_OneHundredTwentyFiveCanExtendBeyondTargetForClipping()
    {
        var result = PhysicalScaleCalculator.Calculate(
            sourcePixelWidth: 2100,
            sourcePixelHeight: 2970,
            targetWidthPx: 2000,
            targetHeightPx: 2870,
            targetWidthMm: 200,
            targetHeightMm: 287,
            sourceWidthMm: 210,
            sourceHeightMm: 297,
            scaling: new PhysicalScaleSpec(
                PhysicalScaleMode.Percent,
                Percent: 125));

        Assert.True(result.Destination.Width > 1);
        Assert.True(result.Destination.Height > 1);
        Assert.True(result.Destination.X < 0);
        Assert.True(result.Destination.Y < 0);
    }
}
