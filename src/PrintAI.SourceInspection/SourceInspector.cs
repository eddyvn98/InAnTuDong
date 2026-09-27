using MetadataExtractor;
using PdfSharp.Pdf.IO;
using PrintAI.ImageDecoding;
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
            ".heic" or ".heif" => InspectHeic(path),
            ".pdf" => InspectPdf(path),
            _ => throw new NotSupportedException(
                "Only JPG, JPEG, PNG, HEIC, HEIF and PDF are supported.")
        };
    }

    private static SourceMetadata InspectImage(string path, SourceKind kind)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException("The image could not be decoded.");

        var rawMetadata = ImageMetadataReader.ReadMetadata(path)
            .SelectMany(directory => directory.Tags.Select(tag => new
            {
                Key = $"{directory.Name}.{tag.Name}",
                Value = tag.Description ?? string.Empty
            }))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => string.Join(" | ", group.Select(item => item.Value)),
                StringComparer.OrdinalIgnoreCase);

        return new(
            Path: path,
            Kind: kind,
            PixelWidth: codec.Info.Width,
            PixelHeight: codec.Info.Height,
            Orientation: codec.EncodedOrigin.ToString(),
            RawMetadata: rawMetadata);
    }

    private static SourceMetadata InspectHeic(string path)
    {
        var png = HeicDecoder.DecodeToPng(path);
        using var data = SKData.CreateCopy(png);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("The decoded HEIC image could not be inspected.");

        return new(
            Path: path,
            Kind: SourceKind.Heic,
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
