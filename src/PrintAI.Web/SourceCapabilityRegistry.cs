namespace PrintAI.Web;

public sealed record SourceCapability(string Id, string Description, bool IsAvailable);

public sealed class SourceCapabilityRegistry
{
    private readonly IReadOnlyList<SourceCapability> _capabilities =
    [
        new("text-pdf", "Create printable text-first PDF documents from a natural-language request.", true),
        new("uploaded-images", "Use uploaded image sources and arrange them for printing.", true),
        new("uploaded-pdf", "Use uploaded PDF pages as print sources.", true),
        new("uploaded-office", "Convert supported Word, Excel and PowerPoint documents to PDF sources.", true),
        new("image-generation", "Generate a new image from a natural-language description.", false),
        new("docx-generation", "Generate a native editable Word document.", false),
        new("xlsx-generation", "Generate a native editable Excel workbook.", false),
        new("pptx-generation", "Generate a native editable PowerPoint presentation.", false),
        new("qr-barcode", "Generate QR codes and barcodes as printable sources.", false)
    ];

    public IReadOnlyList<SourceCapability> List() => _capabilities;

    public string DescribeForModel() => string.Join(
        "\n",
        _capabilities.Select(capability =>
            $"- {capability.Id}: {(capability.IsAvailable ? "AVAILABLE" : "UNAVAILABLE")} — {capability.Description}"));
}
