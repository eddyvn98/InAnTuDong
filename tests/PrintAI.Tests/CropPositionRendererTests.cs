using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class CropPositionRendererTests
{
    [Fact]
    public void AutoTrimWhite_ExpandsContentAcrossTargetPlacement()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(100, 100))
            {
                bitmap.Erase(SKColors.White);
                using var canvas = new SKCanvas(bitmap);
                using var paint = new SKPaint
                {
                    Color = SKColors.Red
                };
                canvas.DrawRect(
                    new SKRect(25, 25, 75, 75),
                    paint);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(
                    SKEncodedImageFormat.Png,
                    100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "auto trim",
                [
                    new SourceSpec(path)
                ],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.ExactSize,
                    ItemWidthMm: 100,
                    ItemHeightMm: 100,
                    MarginMm: 5,
                    AllowRotate: false,
                    Fit: FitMode.Contain,
                    SourceCrop: new SourceCropSpec(
                        SourceCropMode.AutoTrimWhite)),
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

            // Exact-size placement is x=55..155, y=98.5..198.5.
            // Auto-trim should make content reach close to the placement edge.
            var nearLeftInside = rendered.GetPixel(
                Px(60),
                Px(148.5));

            Assert.True(
                nearLeftInside.Red > 220 &&
                nearLeftInside.Green < 80 &&
                nearLeftInside.Blue < 80);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
