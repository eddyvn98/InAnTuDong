namespace PrintAI.SourceInspection;

public enum SourceKind
{
    Jpeg,
    Png,
    Pdf
}

public sealed record SourceMetadata(
    string Path,
    SourceKind Kind,
    int? PixelWidth = null,
    int? PixelHeight = null,
    double? DpiX = null,
    double? DpiY = null,
    string? Orientation = null,
    int? PageCount = null,
    IReadOnlyList<PdfPageMetadata>? Pages = null,
    IReadOnlyDictionary<string, string>? RawMetadata = null);

public sealed record PdfPageMetadata(
    int Page,
    double WidthMm,
    double HeightMm);
