using PrintAI.Domain;

namespace PrintAI.Workflows;

public sealed record MixedCompositionOptions(
    double ItemWidthMm,
    double ItemHeightMm,
    double GapMm = 2,
    double MarginMm = 5,
    bool AllowRotate = true,
    bool CutMarks = false,
    FitMode Fit = FitMode.Contain);

public static class MixedCompositionWorkflow
{
    public const string Id = "mixed-composition";
    public const int MaxItemsPerJob = 1000;

    public static PrintJobSpec CreateJob(
        IReadOnlyList<SourceSpec> sources,
        MixedCompositionOptions options,
        string jobName = "Bố cục nhiều nội dung")
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(options);

        Validate(sources, options);

        return new(
            jobName,
            sources.ToArray(),
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

    private static void Validate(
        IReadOnlyList<SourceSpec> sources,
        MixedCompositionOptions options)
    {
        if (sources.Count == 0)
            throw new ArgumentException("Phải chọn ít nhất một source/page.", nameof(sources));

        if (sources.Any(source => source.Copies < 1))
            throw new ArgumentOutOfRangeException(nameof(sources), "Số bản của mỗi source phải từ 1 trở lên.");

        var totalItems = sources.Sum(source => source.Copies);
        if (totalItems > MaxItemsPerJob)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sources),
                $"Tổng số nội dung trong một job không được vượt quá {MaxItemsPerJob}.");
        }

        if (options.ItemWidthMm <= 0 || options.ItemHeightMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Kích thước nội dung phải lớn hơn 0 mm.");

        if (options.GapMm < 0 || options.MarginMm < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Gap và margin không được âm.");

        var availableWidth = 210 - (2 * options.MarginMm);
        var availableHeight = 297 - (2 * options.MarginMm);

        if (availableWidth <= 0 || availableHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Margin quá lớn so với khổ A4.");

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
                "Kích thước nội dung không thể đặt vừa vùng A4 với margin hiện tại.",
                nameof(options));
        }
    }
}
