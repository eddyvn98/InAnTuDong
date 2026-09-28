using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class BookletRendererTests
{
    [Fact]
    public void VirtualBlankPadding_RendersWhiteBesideRealPage()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(100, 100))
            {
                bitmap.Erase(SKColors.Red);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(
                    SKEncodedImageFormat.Png,
                    100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var group = new PrintOutputGroupSpec(
                Name: "booklet",
                Selections:
                [
                    new PageSelectionSpec(
                        0,
                        [new PageRangeSpec(1, 1)])
                ],
                Paper: new PaperSpec(),
                Layout: new LayoutSpec(
                    LayoutMode.ExactSize,
                    200,
                    287),
                Print: new OutputPrintSettings(),
                Booklet: new BookletSpec());

            var imposed = BookletImpositionResolver.Resolve(
                group,
                [new SourceSpec(path)]);

            var job = new PrintJobSpec(
                "booklet",
                imposed.Sources,
                imposed.Paper,
                imposed.Layout,
                new PrintSettings(
                    Duplex: imposed.Duplex),
                new PolicySpec());

            var png = SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: 0,
                dpi: 100);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            var leftBlank = rendered.GetPixel(
                Px(70),
                Px(105));
            var rightReal = rendered.GetPixel(
                Px(220),
                Px(105));

            Assert.True(
                leftBlank.Red > 240 &&
                leftBlank.Green > 240 &&
                leftBlank.Blue > 240);

            Assert.True(
                rightReal.Red > 230 &&
                rightReal.Green < 40 &&
                rightReal.Blue < 40);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
