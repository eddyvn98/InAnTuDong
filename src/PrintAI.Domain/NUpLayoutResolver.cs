namespace PrintAI.Domain;

public static class NUpLayoutResolver
{
    public static readonly IReadOnlySet<int> SupportedPagesPerSheet =
        new HashSet<int> { 2, 4, 6, 8, 9, 16 };

    public static (PaperSpec Paper, LayoutSpec Layout) Resolve(
        PrintOutputGroupSpec group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.NUp is null)
            return (group.Paper, group.Layout);

        var nUp = group.NUp;

        if (!SupportedPagesPerSheet.Contains(nUp.PagesPerSheet))
        {
            throw new ArgumentException(
                $"Unsupported pagesPerSheet {nUp.PagesPerSheet}. " +
                $"Supported values: {string.Join(", ", SupportedPagesPerSheet.Order())}.",
                nameof(group));
        }

        if (nUp.GapMm < 0 || nUp.MarginMm < 0)
        {
            throw new ArgumentException(
                "N-up gap and margin cannot be negative.",
                nameof(group));
        }

        var orientation = nUp.AutoOrientation
            ? RecommendedOrientation(nUp.PagesPerSheet)
            : group.Paper.Orientation;

        var paper = group.Paper with
        {
            Orientation = orientation
        };

        var (columns, rows) = GridFor(
            nUp.PagesPerSheet,
            orientation);

        var paperWidth = orientation == PageOrientation.Portrait
            ? paper.WidthMm
            : paper.HeightMm;
        var paperHeight = orientation == PageOrientation.Portrait
            ? paper.HeightMm
            : paper.WidthMm;

        var usableWidth =
            paperWidth -
            (2 * nUp.MarginMm) -
            ((columns - 1) * nUp.GapMm);

        var usableHeight =
            paperHeight -
            (2 * nUp.MarginMm) -
            ((rows - 1) * nUp.GapMm);

        if (usableWidth <= 0 || usableHeight <= 0)
        {
            throw new ArgumentException(
                "N-up margin/gap leaves no printable cell area.",
                nameof(group));
        }

        var layout = new LayoutSpec(
            Mode: LayoutMode.Grid,
            ItemWidthMm: usableWidth / columns,
            ItemHeightMm: usableHeight / rows,
            GapMm: nUp.GapMm,
            MarginMm: nUp.MarginMm,
            AllowRotate: false,
            CutMarks: false,
            Fit: nUp.Fit,
            Canvas: null,
            ItemBorder: nUp.Border);

        return (paper, layout);
    }

    public static (int Columns, int Rows) GridFor(
        int pagesPerSheet,
        PageOrientation orientation) =>
        pagesPerSheet switch
        {
            2 => orientation == PageOrientation.Portrait
                ? (1, 2)
                : (2, 1),
            4 => (2, 2),
            6 => orientation == PageOrientation.Portrait
                ? (2, 3)
                : (3, 2),
            8 => orientation == PageOrientation.Portrait
                ? (2, 4)
                : (4, 2),
            9 => (3, 3),
            16 => (4, 4),
            _ => throw new ArgumentOutOfRangeException(
                nameof(pagesPerSheet),
                pagesPerSheet,
                "Unsupported pages-per-sheet value.")
        };

    public static PageOrientation RecommendedOrientation(
        int pagesPerSheet) =>
        pagesPerSheet switch
        {
            2 or 6 or 8 => PageOrientation.Landscape,
            4 or 9 or 16 => PageOrientation.Portrait,
            _ => throw new ArgumentOutOfRangeException(
                nameof(pagesPerSheet),
                pagesPerSheet,
                "Unsupported pages-per-sheet value.")
        };
}
