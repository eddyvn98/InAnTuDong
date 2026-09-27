using PdfSharp.Pdf;
using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class PdfPageRasterizerTests
{
    [Fact]
    public void RenderPage_RasterizesRequestedPdfPage()
    {
        var path = CreateTwoPagePdf();

        try
        {
            using var bitmap = PdfPageRasterizer.RenderPage(path, pageIndex: 1, dpi: 72);

            Assert.InRange(bitmap.Width, 590, 600);
            Assert.InRange(bitmap.Height, 838, 846);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SourcePagePreview_RendersPdfPageOntoA4()
    {
        var path = CreateTwoPagePdf();

        try
        {
            var job = new PrintJobSpec(
                "pdf-preview",
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

            var png = SourcePagePreview.RenderA4(
                job,
                path,
                pageIndex: 0,
                dpi: 72);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);
            Assert.Equal(595, rendered.Width);
            Assert.Equal(842, rendered.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTwoPagePdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        using var document = new PdfDocument();
        for (var i = 0; i < 2; i++)
        {
            var page = document.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromMillimeter(210);
            page.Height = PdfSharp.Drawing.XUnit.FromMillimeter(297);
        }

        document.Save(path);
        return path;
    }
}
