using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class A4PreviewRendererTests
{
    [Fact]
    public void RenderPng_ProducesA4BitmapWithSourceContent()
    {
        var job = new PrintJobSpec(
            "preview",
            [new SourceSpec("sample.png")],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                MarginMm: 5,
                AllowRotate: false,
                CutMarks: true,
                Fit: FitMode.Cover),
            new PrintSettings(),
            new PolicySpec());

        var layout = LayoutEngine.Layout(job);
        using var source = new SKBitmap(100, 100);
        source.Erase(SKColors.Red);

        var png = A4PreviewRenderer.RenderPng(job, layout, source, dpi: 96);

        using var rendered = SKBitmap.Decode(png);
        Assert.NotNull(rendered);
        Assert.Equal(794, rendered.Width);
        Assert.Equal(1123, rendered.Height);

        var center = rendered.GetPixel(rendered.Width / 2, rendered.Height / 2);
        Assert.True(center.Red > 240);
        Assert.True(center.Green < 20);
        Assert.True(center.Blue < 20);
    }
}
