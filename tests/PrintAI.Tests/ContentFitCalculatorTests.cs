using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class ContentFitCalculatorTests
{
    [Fact]
    public void Contain_PreservesWholeSourceAndLetterboxes()
    {
        var result = ContentFitCalculator.Calculate(
            sourceWidth: 200,
            sourceHeight: 100,
            targetWidth: 100,
            targetHeight: 100,
            FitMode.Contain);

        Assert.Equal(new NormalizedRect(0, 0, 1, 1), result.Source);
        Assert.Equal(0, result.Destination.X, 6);
        Assert.Equal(0.25, result.Destination.Y, 6);
        Assert.Equal(1, result.Destination.Width, 6);
        Assert.Equal(0.5, result.Destination.Height, 6);
    }

    [Fact]
    public void Cover_FillsTargetAndCropsSource()
    {
        var result = ContentFitCalculator.Calculate(
            sourceWidth: 200,
            sourceHeight: 100,
            targetWidth: 100,
            targetHeight: 100,
            FitMode.Cover);

        Assert.Equal(new NormalizedRect(0, 0, 1, 1), result.Destination);
        Assert.Equal(0.25, result.Source.X, 6);
        Assert.Equal(0, result.Source.Y, 6);
        Assert.Equal(0.5, result.Source.Width, 6);
        Assert.Equal(1, result.Source.Height, 6);
    }
}
