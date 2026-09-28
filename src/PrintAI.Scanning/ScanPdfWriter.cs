using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PrintAI.Scanning;

public static class ScanPdfWriter
{
    public static string Write(
        IEnumerable<string> imagePaths,
        string outputPath) =>
        Write(
            imagePaths,
            outputPath,
            pageWidthMm: 210,
            pageHeightMm: 297);

    public static string Write(
        IEnumerable<string> imagePaths,
        string outputPath,
        double pageWidthMm,
        double pageHeightMm)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (pageWidthMm <= 0 || pageHeightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageWidthMm),
                "PDF page dimensions must be positive.");
        }

        var paths = imagePaths.ToArray();
        if (paths.Length == 0)
            throw new ArgumentException("At least one scanned image is required.", nameof(imagePaths));

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var document = new PdfDocument();

        foreach (var path in paths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Scanned image does not exist.", path);

            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(pageWidthMm);
            page.Height = XUnit.FromMillimeter(pageHeightMm);

            using var image = XImage.FromFile(path);
            using var graphics = XGraphics.FromPdfPage(page);

            var pageWidth = page.Width.Point;
            var pageHeight = page.Height.Point;
            var scale = Math.Min(
                pageWidth / image.PointWidth,
                pageHeight / image.PointHeight);

            var width = image.PointWidth * scale;
            var height = image.PointHeight * scale;
            var x = (pageWidth - width) / 2;
            var y = (pageHeight - height) / 2;

            graphics.DrawImage(image, x, y, width, height);
        }

        document.Save(outputPath);
        return outputPath;
    }
}
