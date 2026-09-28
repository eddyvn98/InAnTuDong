using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class PhysicalScalingRendererTests
{
    [Fact]
    public void FiftyPercent_RendersHalfPhysicalSizeCenteredInsidePlacement()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(200, 200))
            {
                bitmap.Erase(SKColors.Red);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(
                    SKEncodedImageFormat.Png,
                    100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "50 percent",
                [
                    new SourceSpec(
                        path,
                        OriginalWidthMm: 100,
                        OriginalHeightMm: 100)
                ],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.ExactSize,
                    ItemWidthMm: 100,
                    ItemHeightMm: 100,
                    MarginMm: 5,
                    AllowRotate: false,
                    Fit: FitMode.Contain,
                    PhysicalScale: new PhysicalScaleSpec(
                        PhysicalScaleMode.Percent,
                        Percent: 50)),
                new PrintSettings(),
                new PolicySpec());

            var png = SourceJobRenderer.RenderA4(
                job,
                path,
                sourcePageIndex: 0,
                outputPageIndex: 0,
                dpi: 100);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            // Exact-size 100x100 placement is centered on A4:
            // x=55..155, y=98.5..198.5. At 50%, red content is
            // x=80..130, y=123.5..173.5.
            var center = rendered.GetPixel(
                Px(105),
                Px(148.5));
            var insidePlacementOutsideScaledContent =
                rendered.GetPixel(
                    Px(60),
                    Px(110));

            Assert.True(
                center.Red > 240 &&
                center.Green < 30 &&
                center.Blue < 30);

            Assert.True(
                insidePlacementOutsideScaledContent.Red > 240 &&
                insidePlacementOutsideScaledContent.Green > 240 &&
                insidePlacementOutsideScaledContent.Blue > 240);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
