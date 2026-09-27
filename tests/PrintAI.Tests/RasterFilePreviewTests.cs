using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class RasterFilePreviewTests
{
    [Fact]
    public void RenderA4_FromPngFile_ProducesExpectedPaperSize()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(120, 80))
            {
                bitmap.Erase(SKColors.DarkGreen);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "desktop-preview",
                [new SourceSpec(path)],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.Grid,
                    ItemWidthMm: 200,
                    ItemHeightMm: 287,
                    MarginMm: 5,
                    AllowRotate: false,
                    Fit: FitMode.Contain),
                new PrintSettings(),
                new PolicySpec());

            var png = RasterFilePreview.RenderA4(job, path, dpi: 127);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);
            Assert.Equal(1050, rendered.Width);
            Assert.Equal(1485, rendered.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
