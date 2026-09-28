namespace PrintAI.Domain;

public sealed record BookletImpositionResult(
    IReadOnlyList<SourceSpec> Sources,
    PaperSpec Paper,
    LayoutSpec Layout,
    DuplexMode Duplex,
    int LogicalPageCount,
    int PaddedPageCount,
    int SheetCount);

public static class BookletImpositionResolver
{
    public static BookletImpositionResult Resolve(
        PrintOutputGroupSpec group,
        IReadOnlyList<SourceSpec> logicalSources)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(logicalSources);

        var booklet = group.Booklet
            ?? throw new ArgumentException(
                "Booklet intent is required.",
                nameof(group));

        if (logicalSources.Count == 0)
        {
            throw new ArgumentException(
                "Booklet requires at least one logical source page.",
                nameof(logicalSources));
        }

        if (!double.IsFinite(booklet.GutterMm) ||
            booklet.GutterMm < 0 ||
            !double.IsFinite(booklet.MarginMm) ||
            booklet.MarginMm < 0)
        {
            throw new ArgumentException(
                "Booklet gutter and margin must be finite and non-negative.",
                nameof(group));
        }

        var paddedPageCount =
            ((logicalSources.Count + 3) / 4) * 4;

        var imposed = new List<SourceSpec>(
            paddedPageCount);

        var sheetCount = paddedPageCount / 4;

        for (var sheet = 0; sheet < sheetCount; sheet++)
        {
            var frontLeft = paddedPageCount - (sheet * 2);
            var frontRight = 1 + (sheet * 2);
            var backLeft = 2 + (sheet * 2);
            var backRight = paddedPageCount - 1 - (sheet * 2);

            imposed.Add(PageOrBlank(
                logicalSources,
                frontLeft));
            imposed.Add(PageOrBlank(
                logicalSources,
                frontRight));
            imposed.Add(PageOrBlank(
                logicalSources,
                backLeft));
            imposed.Add(PageOrBlank(
                logicalSources,
                backRight));
        }

        var paper = group.Paper with
        {
            Orientation = PageOrientation.Landscape
        };

        var paperWidth = paper.HeightMm;
        var paperHeight = paper.WidthMm;
        var itemWidth =
            (paperWidth -
             (2 * booklet.MarginMm) -
             booklet.GutterMm) / 2;
        var itemHeight =
            paperHeight -
            (2 * booklet.MarginMm);

        if (itemWidth <= 0 || itemHeight <= 0)
        {
            throw new ArgumentException(
                "Booklet margin/gutter leaves no printable page area.",
                nameof(group));
        }

        var layout = new LayoutSpec(
            Mode: LayoutMode.Grid,
            ItemWidthMm: itemWidth,
            ItemHeightMm: itemHeight,
            GapMm: booklet.GutterMm,
            MarginMm: booklet.MarginMm,
            AllowRotate: false,
            CutMarks: false,
            Fit: FitMode.Contain);

        return new(
            imposed,
            paper,
            layout,
            DuplexMode.ShortEdge,
            logicalSources.Count,
            paddedPageCount,
            sheetCount);
    }

    private static SourceSpec PageOrBlank(
        IReadOnlyList<SourceSpec> logicalSources,
        int oneBasedPage)
    {
        if (oneBasedPage >= 1 &&
            oneBasedPage <= logicalSources.Count)
        {
            return logicalSources[oneBasedPage - 1] with
            {
                Copies = 1
            };
        }

        return new SourceSpec(
            Path: "",
            Copies: 1,
            PageIndex: 0,
            IsBlank: true);
    }
}
