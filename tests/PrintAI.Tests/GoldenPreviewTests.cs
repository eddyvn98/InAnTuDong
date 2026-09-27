using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class GoldenPreviewTests
{
    [Fact]
    public void ContainPreview_MatchesSemanticGoldenSnapshot()
    {
        var job = new PrintJobSpec(
            "golden",
            [new SourceSpec("two-tone.png")],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 42,
                ItemHeightMm: 59,
                MarginMm: 5,
                AllowRotate: false,
                CutMarks: false,
                Fit: FitMode.Contain),
            new PrintSettings(),
            new PolicySpec());

        var layout = LayoutEngine.Layout(job);
        using var source = CreateTwoToneSource();
        var png = A4PreviewRenderer.RenderPng(job, layout, source, dpi: 127);

        using var rendered = SKBitmap.Decode(png);
        Assert.NotNull(rendered);

        var snapshot = string.Join(
            "|",
            $"{rendered.Width}x{rendered.Height}",
            Classify(rendered.GetPixel(100, 100)),
            Classify(rendered.GetPixel(450, 650)),
            Classify(rendered.GetPixel(450, 720)),
            Classify(rendered.GetPixel(600, 720)),
            Classify(rendered.GetPixel(450, 850)),
            Classify(rendered.GetPixel(900, 1200)));

        Assert.Equal("1050x1485|W|W|R|B|W|W", snapshot);
    }

    private static SKBitmap CreateTwoToneSource()
    {
        var bitmap = new SKBitmap(200, 100);

        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            bitmap.SetPixel(x, y, x < 100 ? SKColors.Red : SKColors.Blue);

        return bitmap;
    }

    private static string Classify(SKColor color)
    {
        if (color.Red > 240 && color.Green > 240 && color.Blue > 240)
            return "W";

        if (color.Red > 240 && color.Green < 20 && color.Blue < 20)
            return "R";

        if (color.Blue > 240 && color.Red < 20 && color.Green < 20)
            return "B";

        return $"X({color.Red},{color.Green},{color.Blue})";
    }
}
