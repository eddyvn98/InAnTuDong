using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class SourceJobRendererTests
{
    [Fact]
    public void TwentyFourBySixCmCopies_ExposeEveryOutputPage()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(120, 180))
            {
                bitmap.Erase(SKColors.DarkOrange);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(
                    SKEncodedImageFormat.Png,
                    100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "20 copies",
                [new SourceSpec(path, Copies: 20)],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.Grid,
                    40,
                    60,
                    GapMm: 3,
                    MarginMm: 5,
                    AllowRotate: true,
                    CutMarks: true,
                    Fit: FitMode.Cover),
                new PrintSettings(),
                new PolicySpec());

            Assert.Equal(2, SourceJobRenderer.GetOutputPageCount(job));

            var first = SourceJobRenderer.RenderA4(
                job, path, 0, 0, dpi: 72);
            var second = SourceJobRenderer.RenderA4(
                job, path, 0, 1, dpi: 72);

            Assert.NotEmpty(first);
            Assert.NotEmpty(second);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
