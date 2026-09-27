using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class CalibrationPageRendererTests
{
    [Fact]
    public void CalibrationPage_IsA4AndContainsReferenceGeometry()
    {
        var png = CalibrationPageRenderer.RenderA4Png(dpi: 127);

        using var rendered = SKBitmap.Decode(png);
        Assert.NotNull(rendered);
        Assert.Equal(1050, rendered.Width);
        Assert.Equal(1485, rendered.Height);

        var nonWhite = 0;
        for (var y = 0; y < rendered.Height; y += 10)
        for (var x = 0; x < rendered.Width; x += 10)
        {
            var pixel = rendered.GetPixel(x, y);
            if (pixel.Red < 250 || pixel.Green < 250 || pixel.Blue < 250)
                nonWhite++;
        }

        Assert.True(nonWhite > 100);
    }

    [Theory]
    [InlineData(71)]
    [InlineData(601)]
    public void CalibrationPage_RejectsUnsafeDpi(int dpi)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalibrationPageRenderer.RenderA4Png(dpi));
    }
}
