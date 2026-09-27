namespace PrintAI.Windows.Printing;

public sealed record PrinterCapabilitySnapshot(
    string Name,
    bool IsDefault,
    bool IsValid,
    bool SupportsColor,
    bool CanDuplex,
    IReadOnlyList<PaperCapability> PaperSizes,
    IReadOnlyList<ResolutionCapability> Resolutions,
    PageCapability? A4Portrait,
    string? Error = null);

public sealed record PaperCapability(
    string Name,
    string Kind,
    int RawKind,
    double WidthMm,
    double HeightMm);

public sealed record ResolutionCapability(
    string Kind,
    int X,
    int Y);

public sealed record PageCapability(
    double PaperWidthMm,
    double PaperHeightMm,
    double PrintableXmm,
    double PrintableYmm,
    double PrintableWidthMm,
    double PrintableHeightMm,
    double HardMarginXmm,
    double HardMarginYmm);
