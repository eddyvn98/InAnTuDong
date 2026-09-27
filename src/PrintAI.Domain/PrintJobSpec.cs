namespace PrintAI.Domain;

public sealed record SourceSpec(string Path, int Copies = 1, int PageIndex = 0);

public sealed record PaperSpec(
    double WidthMm = 210,
    double HeightMm = 297,
    PageOrientation Orientation = PageOrientation.Portrait);

public sealed record LayoutSpec(
    LayoutMode Mode,
    double ItemWidthMm,
    double ItemHeightMm,
    double GapMm = 0,
    double MarginMm = 5,
    bool AllowRotate = true,
    bool CutMarks = false,
    FitMode Fit = FitMode.Contain);

public sealed record PrintSettings(
    int Copies = 1,
    ColorMode ColorMode = ColorMode.Color,
    PrintQuality Quality = PrintQuality.Standard,
    DuplexMode Duplex = DuplexMode.Off);

public sealed record PolicySpec(PreviewPolicy Preview = PreviewPolicy.Required);

public sealed record PrintJobSpec(
    string JobName,
    IReadOnlyList<SourceSpec> Sources,
    PaperSpec Paper,
    LayoutSpec Layout,
    PrintSettings Print,
    PolicySpec Policy,
    string SchemaVersion = "1.0");

public enum PageOrientation { Portrait, Landscape }
public enum LayoutMode { Grid, ExactSize }
public enum FitMode { Contain, Cover }
public enum ColorMode { Color, Grayscale }
public enum PrintQuality { Draft, Standard, High }
public enum DuplexMode { Off, LongEdge, ShortEdge }
public enum PreviewPolicy { Required, Smart, Direct }
