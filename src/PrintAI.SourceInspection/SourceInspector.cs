using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace PrintAI.SourceInspection;

public static class SourceInspector
{
    public static SourceMetadata Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("Source file does not exist.", path);

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => InspectImage(path, SourceKind.Jpeg),
            ".png" => InspectImage(path, SourceKind.Png),
            ".pdf" => InspectPdf(path),
            _ => throw new NotSupportedException("Only JPG, JPEG, PNG and PDF are supported in M1.")
        };
    }

    private static SourceMetadata InspectImage(string path, SourceKind kind)
    {
        using var data = SKData.Create(path);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("The image could not be decoded.");

        return new(
            Path: path,
            Kind: kind,
            PixelWidth: codec.Info.Width,
            PixelHeight: codec.Info.Height,
            Orientation: codec.EncodedOrigin.ToString());
    }

    private static SourceMetadata InspectPdf(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var pages = document.Pages
            .Cast<PdfSharp.Pdf.PdfPage>()
            .Select((page, index) => new PdfPageMetadata(
                Page: index,
                WidthMm: PointsToMm(page.Width.Point),
                HeightMm: PointsToMm(page.Height.Point)))
            .ToArray();

        return new(
            Path: path,
            Kind: SourceKind.Pdf,
            PageCount: pages.Length,
            Pages: pages);
    }

    private static double PointsToMm(double points) => points / 72d * 25.4;
}
