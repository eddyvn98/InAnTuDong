namespace PrintAI.Domain;

public sealed record PosterTileSourceSpec(
    int Row,
    int Column,
    int Rows,
    int Columns,
    double TargetWidthMm,
    double TargetHeightMm,
    double CanvasXmm,
    double CanvasYmm,
    double CanvasWidthMm,
    double CanvasHeightMm,
    FitMode Fit = FitMode.Contain,
    bool RegistrationMarks = false,
    bool TileLabel = false);

public sealed record SourceSpec(
    string Path,
    int Copies = 1,
    int PageIndex = 0,
    double? OriginalWidthMm = null,
    double? OriginalHeightMm = null,
    bool IsBlank = false,
    PosterTileSourceSpec? PosterTile = null);

public sealed record PaperSpec(
    double WidthMm = 210,
    double HeightMm = 297,
    PageOrientation Orientation = PageOrientation.Portrait);

public sealed record ImageTransformSpec(
    double Scale = 1,
    double OffsetX = 0,
    double OffsetY = 0);

public sealed record ShapeSpec(
    FrameShape Kind = FrameShape.Rectangle,
    double CornerRadiusMm = 0);

public sealed record CanvasPlacementSpec(
    int SourceIndex,
    double XMm,
    double YMm,
    double WidthMm,
    double HeightMm,
    double RotationDegrees = 0,
    int ZIndex = 0,
    ShapeSpec? Shape = null,
    ImageTransformSpec? Transform = null,
    FitMode Fit = FitMode.Cover);

public sealed record CanvasLayoutSpec(
    IReadOnlyList<CanvasPlacementSpec> Placements);

public sealed record PhysicalScaleSpec(
    PhysicalScaleMode Mode,
    double Percent = 100);

public sealed record PageMarginsSpec(
    double LeftMm = 5,
    double TopMm = 5,
    double RightMm = 5,
    double BottomMm = 5);

public sealed record PagePlacementSpec(
    PageMarginsSpec Margins,
    PageAnchor Anchor = PageAnchor.Center,
    double OffsetXMm = 0,
    double OffsetYMm = 0,
    bool ShrinkToFit = true);

public sealed record CropEdgesSpec(
    double LeftMm = 0,
    double TopMm = 0,
    double RightMm = 0,
    double BottomMm = 0);

public sealed record SourceCropSpec(
    SourceCropMode Mode,
    CropEdgesSpec? EdgesMm = null,
    byte WhiteThreshold = 245);

public sealed record LayoutSpec(
    LayoutMode Mode,
    double ItemWidthMm,
    double ItemHeightMm,
    double GapMm = 0,
    double MarginMm = 5,
    bool AllowRotate = true,
    bool CutMarks = false,
    FitMode Fit = FitMode.Contain,
    CanvasLayoutSpec? Canvas = null,
    bool ItemBorder = false,
    PhysicalScaleSpec? PhysicalScale = null,
    PagePlacementSpec? PagePlacement = null,
    SourceCropSpec? SourceCrop = null);

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
public enum LayoutMode { Grid, ExactSize, Canvas, PosterTile }
public enum FitMode { Contain, Cover }
public enum PhysicalScaleMode { MaxFit, ShrinkOnly, Percent }
public enum PageAnchor
{
    Center,
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
public enum SourceCropMode
{
    AutoTrimWhite,
    CenterToTargetAspect,
    EdgesMm
}
public enum FrameShape { Rectangle, RoundedRectangle, Ellipse, Circle }
public enum ColorMode { Color, Grayscale }
public enum PrintQuality { Draft, Standard, High }
public enum DuplexMode { Off, LongEdge, ShortEdge }
public enum PreviewPolicy { Required, Smart, Direct }
