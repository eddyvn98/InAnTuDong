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
        var (x, y) = ResolvePosition(
            job,
            paperWidth,
            paperHeight,
            selected.WidthMm,
            selected.HeightMm);

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
        var margins = ResolveMargins(job);
        var availableWidth =
            paperWidth - margins.LeftMm - margins.RightMm;
        var availableHeight =
            paperHeight - margins.TopMm - margins.BottomMm;

        var fits =
            width <= availableWidth &&
            height <= availableHeight;

        if (!fits &&
            job.Layout.PagePlacement?.ShrinkToFit == true &&
            availableWidth > 0 &&
            availableHeight > 0)
        {
            var scale = Math.Min(
                1d,
                Math.Min(
                    availableWidth / width,
                    availableHeight / height));

            width *= scale;
            height *= scale;
            fits =
                width <= availableWidth + 0.0001 &&
                height <= availableHeight + 0.0001;
        }

        return new Candidate(
            width,
            height,
            rotated,
            fits);
    }

    private static PageMarginsSpec ResolveMargins(
        PrintJobSpec job) =>
        job.Layout.PagePlacement?.Margins ??
        new PageMarginsSpec(
            job.Layout.MarginMm,
            job.Layout.MarginMm,
            job.Layout.MarginMm,
            job.Layout.MarginMm);

    private static (double X, double Y) ResolvePosition(
        PrintJobSpec job,
        double paperWidth,
        double paperHeight,
        double width,
        double height)
    {
        var placement = job.Layout.PagePlacement;
        if (placement is null)
        {
            return (
                (paperWidth - width) / 2,
                (paperHeight - height) / 2);
        }

        var margins = placement.Margins;
        var left = margins.LeftMm;
        var top = margins.TopMm;
        var right = paperWidth - margins.RightMm;
        var bottom = paperHeight - margins.BottomMm;

        var centerX = left + ((right - left - width) / 2);
        var centerY = top + ((bottom - top - height) / 2);
        var rightX = right - width;
        var bottomY = bottom - height;

        var (x, y) = placement.Anchor switch
        {
            PageAnchor.Top => (centerX, top),
            PageAnchor.Bottom => (centerX, bottomY),
            PageAnchor.Left => (left, centerY),
            PageAnchor.Right => (rightX, centerY),
            PageAnchor.TopLeft => (left, top),
            PageAnchor.TopRight => (rightX, top),
            PageAnchor.BottomLeft => (left, bottomY),
            PageAnchor.BottomRight => (rightX, bottomY),
            _ => (centerX, centerY)
        };

        x += placement.OffsetXMm;
        y += placement.OffsetYMm;

        if (x < -0.0001 ||
            y < -0.0001 ||
            x + width > paperWidth + 0.0001 ||
            y + height > paperHeight + 0.0001)
        {
            throw new InvalidOperationException(
                "Page placement offset moves the item outside the physical paper.");
        }

        return (x, y);
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
