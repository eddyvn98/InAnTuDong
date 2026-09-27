using PrintAI.Domain;

namespace PrintAI.Workflows;

public sealed record LabelSheetOptions(
    double ItemWidthMm,
    double ItemHeightMm,
    int Copies,
    double GapMm = 2,
    double MarginMm = 5,
    bool AllowRotate = true,
    bool CutMarks = true,
    FitMode Fit = FitMode.Contain);

public static class LabelSheetWorkflow
{
    public const string CustomId = "label-custom-sheet";

    public static PrintJobSpec CreateJob(
        SourceSpec source,
        LabelSheetOptions options,
        string jobName = "Label sheet tùy chỉnh")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        Validate(options);

        return new(
            jobName,
            [source with { Copies = options.Copies }],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: options.ItemWidthMm,
                ItemHeightMm: options.ItemHeightMm,
                GapMm: options.GapMm,
                MarginMm: options.MarginMm,
                AllowRotate: options.AllowRotate,
                CutMarks: options.CutMarks,
                Fit: options.Fit),
            new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.Standard),
            new PolicySpec(PreviewPolicy.Required));
    }

    private static void Validate(LabelSheetOptions options)
    {
        if (options.ItemWidthMm <= 0 || options.ItemHeightMm <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Kích thước label phải lớn hơn 0 mm.");

        if (options.Copies is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Số lượng label phải từ 1 đến 1000.");

        if (options.GapMm < 0 || options.MarginMm < 0)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Gap và margin không được âm.");

        var availableWidth = 210 - (2 * options.MarginMm);
        var availableHeight = 297 - (2 * options.MarginMm);

        if (availableWidth <= 0 || availableHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Margin quá lớn so với khổ A4.");
        }

        var fitsNormally =
            options.ItemWidthMm <= availableWidth &&
            options.ItemHeightMm <= availableHeight;

        var fitsRotated =
            options.AllowRotate &&
            options.ItemHeightMm <= availableWidth &&
            options.ItemWidthMm <= availableHeight;

        if (!fitsNormally && !fitsRotated)
        {
            throw new ArgumentException(
                "Kích thước label không thể đặt vừa vùng A4 với margin hiện tại.",
                nameof(options));
        }
    }
}
