namespace PrintAI.Domain;

public sealed record SourcePageSizeSpec(
    int PageIndex,
    double WidthMm,
    double HeightMm);

public sealed record PlanSourceSpec(
    string Path,
    int PageCount = 1,
    IReadOnlyList<SourcePageSizeSpec>? Pages = null,
    int? PixelWidth = null,
    int? PixelHeight = null);

public sealed record PageRangeSpec(
    int StartPage,
    int EndPage);

public sealed record PageSelectionSpec(
    int SourceIndex,
    IReadOnlyList<PageRangeSpec>? Include = null,
    IReadOnlyList<PageRangeSpec>? Exclude = null,
    PageParity Parity = PageParity.All);

public sealed record OutputPrintSettings(
    ColorMode ColorMode = ColorMode.Color,
    PrintQuality Quality = PrintQuality.Standard,
    DuplexMode Duplex = DuplexMode.Off);

public sealed record PosterSpec(
    double? TargetWidthMm = null,
    double? TargetHeightMm = null,
    int? Columns = null,
    int? Rows = null,
    double OverlapMm = 5,
    double MarginMm = 5,
    bool RegistrationMarks = false,
    bool TileLabels = false,
    bool AutoOrientation = true,
    FitMode Fit = FitMode.Contain);

public sealed record BookletSpec(
    double GutterMm = 4,
    double MarginMm = 5);

public sealed record NUpSpec(
    int PagesPerSheet,
    int? Columns = null,
    double GapMm = 2,
    double MarginMm = 5,
    bool Border = false,
    FitMode Fit = FitMode.Contain,
    bool AutoOrientation = true);

public sealed record PrintOutputGroupSpec(
    string Name,
    IReadOnlyList<PageSelectionSpec> Selections,
    PaperSpec Paper,
    LayoutSpec Layout,
    OutputPrintSettings Print,
    int Sets = 1,
    bool Collate = true,
    int Sequence = 0,
    NUpSpec? NUp = null,
    PhysicalScaleSpec? Scaling = null,
    PagePlacementSpec? Placement = null,
    SourceCropSpec? Crop = null,
    BookletSpec? Booklet = null,
    PosterSpec? Poster = null);

public sealed record PrintPlan(
    string PlanName,
    IReadOnlyList<PlanSourceSpec> Sources,
    IReadOnlyList<PrintOutputGroupSpec> OutputGroups,
    PolicySpec Policy,
    string SchemaVersion = "2.0");

public sealed record CompiledPrintBatch(
    int Sequence,
    int GroupIndex,
    int SetNumber,
    PrintJobSpec Job);

public sealed record CompiledPrintPlan(
    string PlanName,
    IReadOnlyList<CompiledPrintBatch> Batches);

public enum PageParity
{
    All,
    Odd,
    Even
}
