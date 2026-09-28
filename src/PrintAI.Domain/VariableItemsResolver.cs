namespace PrintAI.Domain;

public static class VariableItemsResolver
{
    public static PrintJobSpec Resolve(
        PrintPlan plan,
        PrintOutputGroupSpec group,
        string jobName)
    {
        var spec = group.VariableItems
            ?? throw new ArgumentException("VariableItems intent is required.");

        if (spec.Items.Count == 0)
            throw new ArgumentException("VariableItems requires at least one item.");

        if (!double.IsFinite(spec.GapMm) || spec.GapMm < 0 ||
            !double.IsFinite(spec.MarginMm) || spec.MarginMm < 0)
        {
            throw new ArgumentException("VariableItems gap/margin must be finite and non-negative.");
        }

        var paperWidth = group.Paper.Orientation == PageOrientation.Portrait
            ? group.Paper.WidthMm : group.Paper.HeightMm;
        var paperHeight = group.Paper.Orientation == PageOrientation.Portrait
            ? group.Paper.HeightMm : group.Paper.WidthMm;
        var usableWidth = paperWidth - 2 * spec.MarginMm;
        var usableHeight = paperHeight - 2 * spec.MarginMm;

        if (usableWidth <= 0 || usableHeight <= 0)
            throw new ArgumentException("VariableItems margin leaves no printable area.");

        var totalCopies = spec.Items.Sum(item => Math.Max(0, item.Copies));
        if (totalCopies > 1000)
            throw new ArgumentException("VariableItems supports at most 1000 physical placements per job.");

        var sources = new List<SourceSpec>();
        var pieces = new List<Piece>();

        for (var itemIndex = 0; itemIndex < spec.Items.Count; itemIndex++)
        {
            var item = spec.Items[itemIndex];
            if (item.SourceIndex < 0 || item.SourceIndex >= plan.Sources.Count)
                throw new ArgumentException($"Variable item {itemIndex} references unavailable source.");

            var planSource = plan.Sources[item.SourceIndex];
            if (item.Page < 1 || item.Page > planSource.PageCount)
                throw new ArgumentException($"Variable item {itemIndex} page is outside the source.");

            if (item.Copies < 1)
                throw new ArgumentException($"Variable item {itemIndex} copies must be at least 1.");

            var pageIndex = item.Page - 1;
            var physical = planSource.Pages?.FirstOrDefault(p => p.PageIndex == pageIndex);
            var aspect = ResolveAspect(planSource, physical);
            var (width, height) = ResolveSize(item, physical, aspect, itemIndex);

            var sourceIndex = sources.Count;
            sources.Add(new SourceSpec(
                planSource.Path,
                Copies: 1,
                PageIndex: pageIndex,
                OriginalWidthMm: physical?.WidthMm,
                OriginalHeightMm: physical?.HeightMm));

            for (var copy = 0; copy < item.Copies; copy++)
            {
                pieces.Add(new Piece(
                    sourceIndex,
                    itemIndex,
                    copy,
                    width,
                    height,
                    item.AllowRotate,
                    item.Fit));
            }
        }

        var placements = Pack(
            pieces,
            usableWidth,
            usableHeight,
            spec.MarginMm,
            spec.GapMm);

        var canvas = new CanvasLayoutSpec(
            placements.Select((p, index) => new CanvasPlacementSpec(
                SourceIndex: p.SourceIndex,
                XMm: p.X,
                YMm: p.Y,
                WidthMm: p.Width,
                HeightMm: p.Height,
                RotationDegrees: 0,
                ZIndex: index,
                Fit: p.Fit,
                Page: p.Page,
                UseRotatedFootprint: p.Rotated)).ToArray());

        return new PrintJobSpec(
            JobName: jobName,
            Sources: sources,
            Paper: group.Paper,
            Layout: new LayoutSpec(
                Mode: LayoutMode.Canvas,
                ItemWidthMm: 1,
                ItemHeightMm: 1,
                GapMm: spec.GapMm,
                MarginMm: spec.MarginMm,
                AllowRotate: false,
                CutMarks: spec.CutMarks,
                Fit: FitMode.Contain,
                Canvas: canvas),
            Print: new PrintSettings(
                Copies: 1,
                ColorMode: group.Print.ColorMode,
                Quality: group.Print.Quality,
                Duplex: group.Print.Duplex),
            Policy: plan.Policy,
            SchemaVersion: "1.0");
    }

    private static IReadOnlyList<Packed> Pack(
        IReadOnlyList<Piece> pieces,
        double usableWidth,
        double usableHeight,
        double margin,
        double gap)
    {
        var ordered = pieces
            .OrderByDescending(p => Math.Max(p.Width, p.Height))
            .ThenByDescending(p => p.Width * p.Height)
            .ThenBy(p => p.ItemIndex)
            .ThenBy(p => p.CopyIndex)
            .ToArray();

        var pages = new List<List<Shelf>>();
        var packed = new List<Packed>(pieces.Count);

        foreach (var piece in ordered)
        {
            if (!TryPlaceExisting(piece, pages, usableWidth, margin, gap, packed) &&
                !TryPlaceNewShelf(piece, pages, usableWidth, usableHeight, margin, gap, packed))
            {
                pages.Add([]);
                if (!TryPlaceNewShelf(piece, pages, usableWidth, usableHeight, margin, gap, packed))
                {
                    throw new ArgumentException(
                        $"Variable item {piece.ItemIndex} does not fit on the selected paper.");
                }
            }
        }

        return packed;
    }

    private static bool TryPlaceExisting(
        Piece piece,
        List<List<Shelf>> pages,
        double usableWidth,
        double margin,
        double gap,
        List<Packed> packed)
    {
        for (var page = 0; page < pages.Count; page++)
        {
            foreach (var shelf in pages[page])
            {
                foreach (var option in Orientations(piece))
                {
                    var x = shelf.UsedWidth == 0
                        ? margin
                        : margin + shelf.UsedWidth + gap;

                    if (option.Height <= shelf.Height + 0.001 &&
                        (x - margin) + option.Width <= usableWidth + 0.001)
                    {
                        packed.Add(new(
                            piece.SourceIndex,
                            page,
                            x,
                            shelf.Y,
                            option.Width,
                            option.Height,
                            option.Rotated,
                            piece.Fit));
                        shelf.UsedWidth = (x - margin) + option.Width;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryPlaceNewShelf(
        Piece piece,
        List<List<Shelf>> pages,
        double usableWidth,
        double usableHeight,
        double margin,
        double gap,
        List<Packed> packed)
    {
        if (pages.Count == 0)
            pages.Add([]);

        var page = pages.Count - 1;
        var shelves = pages[page];
        var usedHeight = shelves.Count == 0
            ? 0
            : shelves.Sum(s => s.Height) + gap * shelves.Count;

        foreach (var option in Orientations(piece))
        {
            if (option.Width <= usableWidth + 0.001 &&
                usedHeight + option.Height <= usableHeight + 0.001)
            {
                var y = margin + usedHeight;
                shelves.Add(new Shelf(y, option.Height, option.Width));
                packed.Add(new(
                    piece.SourceIndex,
                    page,
                    margin,
                    y,
                    option.Width,
                    option.Height,
                    option.Rotated,
                    piece.Fit));
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Orientation> Orientations(Piece piece)
    {
        yield return new(piece.Width, piece.Height, false);
        if (piece.AllowRotate && Math.Abs(piece.Width - piece.Height) > 0.001)
            yield return new(piece.Height, piece.Width, true);
    }

    private static (double Width, double Height) ResolveSize(
        VariableItemSpec item,
        SourcePageSizeSpec? physical,
        double? aspect,
        int index)
    {
        if (item.WidthMm is double width && (!double.IsFinite(width) || width <= 0) ||
            item.HeightMm is double height && (!double.IsFinite(height) || height <= 0))
        {
            throw new ArgumentException($"Variable item {index} dimensions must be finite and positive.");
        }

        if (item.WidthMm is double w && item.HeightMm is double h)
            return (w, h);

        if (item.WidthMm is double onlyWidth && aspect is double a1)
            return (onlyWidth, onlyWidth / a1);

        if (item.HeightMm is double onlyHeight && aspect is double a2)
            return (onlyHeight * a2, onlyHeight);

        if (item.WidthMm is null && item.HeightMm is null &&
            physical is not null)
        {
            return (physical.WidthMm, physical.HeightMm);
        }

        throw new ArgumentException(
            $"Variable item {index} needs both dimensions or trusted source aspect/physical size.");
    }

    private static double? ResolveAspect(
        PlanSourceSpec source,
        SourcePageSizeSpec? physical)
    {
        if (physical is { WidthMm: > 0, HeightMm: > 0 })
            return physical.WidthMm / physical.HeightMm;

        if (source.PixelWidth is > 0 && source.PixelHeight is > 0)
            return source.PixelWidth.Value / (double)source.PixelHeight.Value;

        return null;
    }

    private sealed record Piece(
        int SourceIndex, int ItemIndex, int CopyIndex,
        double Width, double Height, bool AllowRotate, FitMode Fit);
    private sealed record Orientation(double Width, double Height, bool Rotated);
    private sealed record Packed(
        int SourceIndex, int Page, double X, double Y,
        double Width, double Height, bool Rotated, FitMode Fit);
    private sealed class Shelf(double y, double height, double usedWidth)
    {
        public double Y { get; } = y;
        public double Height { get; } = height;
        public double UsedWidth { get; set; } = usedWidth;
    }
}
