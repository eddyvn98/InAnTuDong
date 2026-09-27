using PrintAI.Domain;

namespace PrintAI.Layout;

public sealed record Placement(
    int Index,
    int Page,
    double XMm,
    double YMm,
    double WidthMm,
    double HeightMm,
    bool Rotated);

public sealed record LayoutResult(
    int Columns,
    int Rows,
    int CapacityPerPage,
    bool Rotated,
    IReadOnlyList<Placement> Placements);

public static class GridLayoutEngine
{
    public static LayoutResult Layout(PrintJobSpec job)
    {
        var validation = PrintJobValidator.Validate(job);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors.Select(e => e.Message)));

        var totalItems = job.Sources.Sum(s => s.Copies);
        var normal = CalculateCandidate(job, rotated: false);

        var selected = normal;
        if (job.Layout.AllowRotate && job.Layout.ItemWidthMm != job.Layout.ItemHeightMm)
        {
            var rotated = CalculateCandidate(job, rotated: true);
            if (rotated.Capacity > normal.Capacity)
                selected = rotated;
        }

        if (selected.Capacity < 1)
            throw new InvalidOperationException("The requested item does not fit inside the printable layout area.");

        var placements = new List<Placement>(totalItems);
        for (var index = 0; index < totalItems; index++)
        {
            var page = index / selected.Capacity;
            var position = index % selected.Capacity;
            var row = position / selected.Columns;
            var col = position % selected.Columns;

            var x = job.Layout.MarginMm + col * (selected.ItemWidth + job.Layout.GapMm);
            var y = job.Layout.MarginMm + row * (selected.ItemHeight + job.Layout.GapMm);

            placements.Add(new(
                index,
                page,
                x,
                y,
                selected.ItemWidth,
                selected.ItemHeight,
                selected.Rotated));
        }

        return new(selected.Columns, selected.Rows, selected.Capacity, selected.Rotated, placements);
    }

    private static Candidate CalculateCandidate(PrintJobSpec job, bool rotated)
    {
        var paperWidth = job.Paper.Orientation == PageOrientation.Portrait
            ? job.Paper.WidthMm : job.Paper.HeightMm;
        var paperHeight = job.Paper.Orientation == PageOrientation.Portrait
            ? job.Paper.HeightMm : job.Paper.WidthMm;

        var itemWidth = rotated ? job.Layout.ItemHeightMm : job.Layout.ItemWidthMm;
        var itemHeight = rotated ? job.Layout.ItemWidthMm : job.Layout.ItemHeightMm;

        var availableWidth = paperWidth - (2 * job.Layout.MarginMm);
        var availableHeight = paperHeight - (2 * job.Layout.MarginMm);

        var columns = CountFits(availableWidth, itemWidth, job.Layout.GapMm);
        var rows = CountFits(availableHeight, itemHeight, job.Layout.GapMm);

        return new(columns, rows, columns * rows, itemWidth, itemHeight, rotated);
    }

    private static int CountFits(double available, double item, double gap)
    {
        if (available < item)
            return 0;

        return (int)Math.Floor((available + gap) / (item + gap));
    }

    private sealed record Candidate(
        int Columns,
        int Rows,
        int Capacity,
        double ItemWidth,
        double ItemHeight,
        bool Rotated);
}
