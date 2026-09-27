using PdfSharp.Pdf;
using PrintAI.SourceInspection;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class SourceInspectorTests
{
    [Fact]
    public void InspectPng_ReadsPixelDimensions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = new SKBitmap(320, 240);
            bitmap.Erase(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);

            var metadata = SourceInspector.Inspect(path);

            Assert.Equal(SourceKind.Png, metadata.Kind);
            Assert.Equal(320, metadata.PixelWidth);
            Assert.Equal(240, metadata.PixelHeight);
            Assert.False(string.IsNullOrWhiteSpace(metadata.Orientation));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InspectPdf_ReadsPageCountAndPhysicalSize()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            using (var document = new PdfDocument())
            {
                var page = document.AddPage();
                page.Width = PdfSharp.Drawing.XUnit.FromMillimeter(210);
                page.Height = PdfSharp.Drawing.XUnit.FromMillimeter(297);
                document.Save(path);
            }

            var metadata = SourceInspector.Inspect(path);

            Assert.Equal(SourceKind.Pdf, metadata.Kind);
            Assert.Equal(1, metadata.PageCount);
            var pageMetadata = Assert.Single(metadata.Pages!);
            Assert.Equal(210, pageMetadata.WidthMm, 1);
            Assert.Equal(297, pageMetadata.HeightMm, 1);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
