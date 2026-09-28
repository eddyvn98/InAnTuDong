using PrintAI.Domain;

namespace PrintAI.Workflows;

public enum AutoLayoutPreference
{
    Balanced,
    MinCrop,
    Fill
}

public sealed record AutoLayoutCandidate(
    string Id,
    string Title,
    string Description,
    int Columns,
    int Rows,
    double Score,
    PrintJobSpec Job);

public static class AutoLayoutWorkflow
{
    public const string Id = "auto-layout";
    public const int MaxSelectedItems = 6;
    public const double FourBySixWidthMm = 101.6;
    public const double FourBySixHeightMm = 152.4;

    public static IReadOnlyList<AutoLayoutCandidate> Generate4x6(
        IReadOnlyList<SourceSpec> sources,
        AutoLayoutPreference preference = AutoLayoutPreference.Balanced,
        double gapMm = 2,
        double marginMm = 3,
        int maxCandidates = 4) =>
        Generate(
            sources,
            new PaperSpec(FourBySixWidthMm, FourBySixHeightMm),
            preference,
            gapMm,
            marginMm,
            maxCandidates);

    public static IReadOnlyList<AutoLayoutCandidate> Generate(
        IReadOnlyList<SourceSpec> sources,
        PaperSpec paper,
        AutoLayoutPreference preference,
        double gapMm = 2,
        double marginMm = 3,
        int maxCandidates = 4)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var totalItems = sources.Sum(source => source.Copies);
        if (totalItems is < 1 or > MaxSelectedItems)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sources),
                $"Auto layout hỗ trợ từ 1 đến {MaxSelectedItems} nội dung.");
        }

        if (gapMm < 0 || marginMm < 0)
            throw new ArgumentOutOfRangeException(nameof(gapMm), "Gap và margin không được âm.");

        if (maxCandidates < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCandidates));

        var candidates = new List<AutoLayoutCandidate>();

        foreach (var orientation in Enum.GetValues<PageOrientation>())
        {
            var (paperWidth, paperHeight) = orientation == PageOrientation.Portrait
                ? (paper.WidthMm, paper.HeightMm)
                : (paper.HeightMm, paper.WidthMm);

            var usableWidth = paperWidth - (2 * marginMm);
            var usableHeight = paperHeight - (2 * marginMm);

            if (usableWidth <= 0 || usableHeight <= 0)
                continue;

            for (var rows = 1; rows <= totalItems; rows++)
            {
                for (var columns = 1; columns <= totalItems; columns++)
                {
                    var capacity = rows * columns;
                    if (capacity < totalItems || capacity > totalItems + 2)
                        continue;

                    var width = (usableWidth - ((columns - 1) * gapMm)) / columns;
                    var height = (usableHeight - ((rows - 1) * gapMm)) / rows;

                    if (width <= 0 || height <= 0)
                        continue;

                    var score = Score(
                        totalItems,
                        rows,
                        columns,
                        width,
                        height,
                        usableWidth,
                        usableHeight,
                        preference);

                    var fit = preference == AutoLayoutPreference.Fill
                        ? FitMode.Cover
                        : FitMode.Contain;

                    var job = new PrintJobSpec(
                        JobName: "Auto layout 4x6",
                        Sources: sources.ToArray(),
                        Paper: paper with { Orientation = orientation },
                        Layout: new LayoutSpec(
                            LayoutMode.Grid,
                            ItemWidthMm: width,
                            ItemHeightMm: height,
                            GapMm: gapMm,
                            MarginMm: marginMm,
                            AllowRotate: false,
                            CutMarks: false,
                            Fit: fit),
                        Print: new PrintSettings(
                            ColorMode: ColorMode.Color,
                            Quality: PrintQuality.High),
                        Policy: new PolicySpec(PreviewPolicy.Required));

                    candidates.Add(new(
                        Id: $"{orientation.ToString().ToLowerInvariant()}-{columns}x{rows}",
                        Title: TitleFor(rows, columns, totalItems),
                        Description:
                            $"{columns} cột x {rows} hàng · {orientation} · " +
                            $"{width:0.#} x {height:0.#} mm/ảnh · {fit}",
                        Columns: columns,
                        Rows: rows,
                        Score: score,
                        Job: job));
                }
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Rows * candidate.Columns)
            .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(maxCandidates)
            .ToArray();
    }

    private static double Score(
        int totalItems,
        int rows,
        int columns,
        double cellWidth,
        double cellHeight,
        double usableWidth,
        double usableHeight,
        AutoLayoutPreference preference)
    {
        var capacity = rows * columns;
        var occupiedRatio = (double)totalItems / capacity;
        var coverage = totalItems * cellWidth * cellHeight /
                       Math.Max(1, usableWidth * usableHeight);
        var aspect = Math.Max(cellWidth, cellHeight) /
                     Math.Max(1, Math.Min(cellWidth, cellHeight));
        var shapePenalty = Math.Max(0, aspect - 1);
        var balancePenalty = Math.Abs(rows - columns) * 0.04;

        return preference switch
        {
            AutoLayoutPreference.MinCrop =>
                (occupiedRatio * 2.5) + coverage -
                (shapePenalty * 0.35) - balancePenalty,

            AutoLayoutPreference.Fill =>
                (coverage * 3.0) + occupiedRatio -
                (shapePenalty * 0.08) - (balancePenalty * 0.5),

            _ =>
                (occupiedRatio * 2.0) + (coverage * 1.5) -
                (shapePenalty * 0.18) - (balancePenalty * 1.5)
        };
    }

    private static string TitleFor(int rows, int columns, int totalItems)
    {
        if (rows == 1)
            return "Một hàng ngang";

        if (columns == 1)
            return "Một cột dọc";

        if (rows == columns)
            return "Lưới cân bằng";

        if (rows * columns == totalItems)
            return "Lấp đầy lưới";

        return rows > columns
            ? "Bố cục dọc"
            : "Bố cục ngang";
    }
}
