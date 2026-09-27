using PDFtoImage;
using SkiaSharp;

namespace PrintAI.Rendering;

public static class PdfPageRasterizer
{
    public static SKBitmap RenderPage(
        string pdfPath,
        int pageIndex,
        int dpi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("PDF file does not exist.", pdfPath);

        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        if (dpi is < 36 or > 600)
            throw new ArgumentOutOfRangeException(nameof(dpi));

        using var stream = File.OpenRead(pdfPath);
        return Conversion.ToImage(
            stream,
            page: new Index(pageIndex),
            options: new RenderOptions(
                Dpi: dpi,
                BackgroundColor: SKColors.White));
    }
}
