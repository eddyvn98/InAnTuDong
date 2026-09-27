using PrintAI.Domain;

namespace PrintAI.Layout;

public static class ExactSizeLayoutEngine
{
    public static LayoutResult Layout(PrintJobSpec job)
    {
        if (job.Layout.Mode != LayoutMode.ExactSize)
            throw new ArgumentException("ExactSizeLayoutEngine requires LayoutMode.ExactSize.");

        var validation = PrintJobValidator.Validate(job);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors.Select(e => e.Message)));

        var (paperWidth, paperHeight) = GetPaperSize(job.Paper);
        var normal = CandidateFor(job, paperWidth, paperHeight, rotated: false);
        var selected = normal;

        if (!normal.Fits && job.Layout.AllowRotate && job.Layout.ItemWidthMm != job.Layout.ItemHeightMm)
            selected = CandidateFor(job, paperWidth, paperHeight, rotated: true);

        if (!selected.Fits)
            throw new InvalidOperationException("The requested exact-size item does not fit inside the printable layout area.");

        var sourceItems = ExpandSources(job);
        var x = (paperWidth - selected.WidthMm) / 2;
        var y = (paperHeight - selected.HeightMm) / 2;
        var placements = sourceItems
            .Select((sourceItem, index) => new Placement(
                index,
                Page: index,
                XMm: x,
                YMm: y,
                WidthMm: selected.WidthMm,
                HeightMm: selected.HeightMm,
                Rotated: selected.Rotated,
                SourceIndex: sourceItem.SourceIndex,
                SourceCopyIndex: sourceItem.SourceCopyIndex))
            .ToArray();

        return new LayoutResult(
            Columns: 1,
            Rows: 1,
            CapacityPerPage: 1,
            Rotated: selected.Rotated,
            Placements: placements);
    }

    private static IReadOnlyList<SourceItem> ExpandSources(PrintJobSpec job)
    {
        var items = new List<SourceItem>();

        for (var sourceIndex = 0;
             sourceIndex < job.Sources.Count;
             sourceIndex++)
        {
            for (var copyIndex = 0;
                 copyIndex < job.Sources[sourceIndex].Copies;
                 copyIndex++)
            {
                items.Add(new(sourceIndex, copyIndex));
            }
        }

        return items;
    }

    private static Candidate CandidateFor(
        PrintJobSpec job,
        double paperWidth,
        double paperHeight,
        bool rotated)
    {
        var width = rotated ? job.Layout.ItemHeightMm : job.Layout.ItemWidthMm;
        var height = rotated ? job.Layout.ItemWidthMm : job.Layout.ItemHeightMm;
        var availableWidth = paperWidth - (2 * job.Layout.MarginMm);
        var availableHeight = paperHeight - (2 * job.Layout.MarginMm);

        return new Candidate(
            width,
            height,
            rotated,
            width <= availableWidth && height <= availableHeight);
    }

    private static (double Width, double Height) GetPaperSize(PaperSpec paper) =>
        paper.Orientation == PageOrientation.Portrait
            ? (paper.WidthMm, paper.HeightMm)
            : (paper.HeightMm, paper.WidthMm);

    private sealed record SourceItem(
        int SourceIndex,
        int SourceCopyIndex);

    private sealed record Candidate(
        double WidthMm,
        double HeightMm,
        bool Rotated,
        bool Fits);
}
