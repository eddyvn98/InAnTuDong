namespace PrintAI.Domain;

public sealed record PosterTilingResult(
    IReadOnlyList<SourceSpec> Sources,
    PaperSpec Paper,
    LayoutSpec Layout,
    int Columns,
    int Rows,
    double TargetWidthMm,
    double TargetHeightMm,
    double CoverageWidthMm,
    double CoverageHeightMm);

public static class PosterTilingResolver
{
    public static PosterTilingResult Resolve(
        PrintPlan plan,
        PrintOutputGroupSpec group,
        IReadOnlyList<SourceSpec> logicalSources)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(logicalSources);

        var poster = group.Poster
            ?? throw new ArgumentException(
                "Poster intent is required.",
                nameof(group));

        if (logicalSources.Count != 1)
        {
            throw new ArgumentException(
                "Poster tiling currently requires exactly one selected source page.",
                nameof(group));
        }

        ValidatePoster(poster);

        var source = logicalSources[0];
        var sourceAspect = ResolveSourceAspect(
            plan,
            group,
            source);

        var (baseTargetWidth, baseTargetHeight) =
            ResolveTargetSize(
                poster,
                source,
                sourceAspect);

        var orientations = poster.AutoOrientation
            ? new[]
            {
                PageOrientation.Portrait,
                PageOrientation.Landscape
            }
            : new[] { group.Paper.Orientation };

        var candidates = orientations
            .Select(orientation => CreateCandidate(
                group.Paper,
                poster,
                orientation,
                baseTargetWidth,
                baseTargetHeight,
                sourceAspect))
            .OrderBy(candidate => candidate.SheetCount)
            .ThenBy(candidate => candidate.WastedAreaMm2)
            .ThenBy(candidate =>
                candidate.Orientation == group.Paper.Orientation
                    ? 0
                    : 1)
            .ToArray();

        var selected = candidates[0];
        var tiles = new List<SourceSpec>(
            selected.Columns * selected.Rows);

        var strideWidth =
            selected.AvailableWidthMm - poster.OverlapMm;
        var strideHeight =
            selected.AvailableHeightMm - poster.OverlapMm;

        for (var row = 0; row < selected.Rows; row++)
        {
            for (var column = 0;
                 column < selected.Columns;
                 column++)
            {
                var x = column * strideWidth;
                var y = row * strideHeight;
                var width = Math.Min(
                    selected.AvailableWidthMm,
                    selected.TargetWidthMm - x);
                var height = Math.Min(
                    selected.AvailableHeightMm,
                    selected.TargetHeightMm - y);

                if (width <= 0 || height <= 0)
                {
                    throw new ArgumentException(
                        "Requested poster grid creates an empty tile.",
                        nameof(group));
                }

                tiles.Add(source with
                {
                    Copies = 1,
                    PosterTile = new PosterTileSourceSpec(
                        Row: row,
                        Column: column,
                        Rows: selected.Rows,
                        Columns: selected.Columns,
                        TargetWidthMm: selected.TargetWidthMm,
                        TargetHeightMm: selected.TargetHeightMm,
                        CanvasXmm: x,
                        CanvasYmm: y,
                        CanvasWidthMm: width,
                        CanvasHeightMm: height,
                        Fit: poster.Fit,
                        RegistrationMarks:
                            poster.RegistrationMarks,
                        TileLabel:
                            poster.TileLabels)
                });
            }
        }

        var paper = group.Paper with
        {
            Orientation = selected.Orientation
        };

        var layout = new LayoutSpec(
            Mode: LayoutMode.PosterTile,
            ItemWidthMm: selected.AvailableWidthMm,
            ItemHeightMm: selected.AvailableHeightMm,
            MarginMm: poster.MarginMm,
            AllowRotate: false,
            CutMarks: false,
            Fit: poster.Fit);

        return new(
            Sources: tiles,
            Paper: paper,
            Layout: layout,
            Columns: selected.Columns,
            Rows: selected.Rows,
            TargetWidthMm: selected.TargetWidthMm,
            TargetHeightMm: selected.TargetHeightMm,
            CoverageWidthMm: selected.CoverageWidthMm,
            CoverageHeightMm: selected.CoverageHeightMm);
    }

    private static Candidate CreateCandidate(
        PaperSpec paper,
        PosterSpec poster,
        PageOrientation orientation,
        double? requestedTargetWidth,
        double? requestedTargetHeight,
        double? sourceAspect)
    {
        var paperWidth = orientation == PageOrientation.Portrait
            ? paper.WidthMm
            : paper.HeightMm;
        var paperHeight = orientation == PageOrientation.Portrait
            ? paper.HeightMm
            : paper.WidthMm;

        var availableWidth =
            paperWidth - (2 * poster.MarginMm);
        var availableHeight =
            paperHeight - (2 * poster.MarginMm);

        if (availableWidth <= 0 ||
            availableHeight <= 0 ||
            poster.OverlapMm >= availableWidth ||
            poster.OverlapMm >= availableHeight)
        {
            throw new ArgumentException(
                "Poster margin/overlap leaves no printable tile area.",
                nameof(poster));
        }

        double targetWidth;
        double targetHeight;
        int columns;
        int rows;

        if (requestedTargetWidth is null &&
            requestedTargetHeight is null)
        {
            if (poster.Columns is not int fixedColumns ||
                poster.Rows is not int fixedRows)
            {
                throw new ArgumentException(
                    "Poster target size or both rows/columns are required.",
                    nameof(poster));
            }

            columns = fixedColumns;
            rows = fixedRows;
            targetWidth = Coverage(
                columns,
                availableWidth,
                poster.OverlapMm);
            targetHeight = Coverage(
                rows,
                availableHeight,
                poster.OverlapMm);
        }
        else
        {
            targetWidth = requestedTargetWidth!.Value;
            targetHeight = requestedTargetHeight!.Value;

            columns = poster.Columns ??
                CountTiles(
                    targetWidth,
                    availableWidth,
                    poster.OverlapMm);
            rows = poster.Rows ??
                CountTiles(
                    targetHeight,
                    availableHeight,
                    poster.OverlapMm);

            var coverageWidth = Coverage(
                columns,
                availableWidth,
                poster.OverlapMm);
            var coverageHeight = Coverage(
                rows,
                availableHeight,
                poster.OverlapMm);

            if (coverageWidth + 0.001 < targetWidth ||
                coverageHeight + 0.001 < targetHeight)
            {
                throw new ArgumentException(
                    "Requested fixed poster grid is too small for the target size.",
                    nameof(poster));
            }
        }

        var coverageW = Coverage(
            columns,
            availableWidth,
            poster.OverlapMm);
        var coverageH = Coverage(
            rows,
            availableHeight,
            poster.OverlapMm);

        var wasted =
            Math.Max(
                0,
                (coverageW * coverageH) -
                (targetWidth * targetHeight));

        if (requestedTargetWidth is null &&
            requestedTargetHeight is null &&
            sourceAspect is double aspect)
        {
            var posterAspect = targetWidth / targetHeight;
            wasted +=
                Math.Abs(Math.Log(posterAspect / aspect)) *
                targetWidth *
                targetHeight;
        }

        return new(
            orientation,
            availableWidth,
            availableHeight,
            columns,
            rows,
            targetWidth,
            targetHeight,
            coverageW,
            coverageH,
            columns * rows,
            wasted);
    }

    private static (
        double? Width,
        double? Height) ResolveTargetSize(
        PosterSpec poster,
        SourceSpec source,
        double? sourceAspect)
    {
        var width = poster.TargetWidthMm;
        var height = poster.TargetHeightMm;

        if (width is not null && height is not null)
            return (width, height);

        if (width is not null)
        {
            if (sourceAspect is null)
                throw MissingAspect();

            return (width, width / sourceAspect.Value);
        }

        if (height is not null)
        {
            if (sourceAspect is null)
                throw MissingAspect();

            return (height * sourceAspect.Value, height);
        }

        if (poster.Columns is not null &&
            poster.Rows is not null)
        {
            return (null, null);
        }

        if (source.OriginalWidthMm is double sourceWidth &&
            source.OriginalHeightMm is double sourceHeight)
        {
            return (sourceWidth, sourceHeight);
        }

        throw new ArgumentException(
            "Poster target size is required when the source has no trusted physical page size.",
            nameof(poster));

        static ArgumentException MissingAspect() =>
            new(
                "Poster needs trusted source aspect ratio to derive the missing target dimension.",
                nameof(poster));
    }

    private static double? ResolveSourceAspect(
        PrintPlan plan,
        PrintOutputGroupSpec group,
        SourceSpec source)
    {
        if (source.OriginalWidthMm is double widthMm &&
            source.OriginalHeightMm is double heightMm &&
            widthMm > 0 &&
            heightMm > 0)
        {
            return widthMm / heightMm;
        }

        var selection = group.Selections[0];
        var planSource = plan.Sources[
            selection.SourceIndex];

        if (planSource.PixelWidth is int pixelWidth &&
            planSource.PixelHeight is int pixelHeight &&
            pixelWidth > 0 &&
            pixelHeight > 0)
        {
            return pixelWidth / (double)pixelHeight;
        }

        return null;
    }

    private static int CountTiles(
        double target,
        double available,
        double overlap)
    {
        if (target <= available)
            return 1;

        var stride = available - overlap;
        return Math.Max(
            1,
            (int)Math.Ceiling(
                (target - overlap) / stride));
    }

    private static double Coverage(
        int count,
        double available,
        double overlap) =>
        count * available -
        Math.Max(0, count - 1) * overlap;

    private static void ValidatePoster(
        PosterSpec poster)
    {
        if (poster.TargetWidthMm is double width &&
            (!double.IsFinite(width) || width <= 0))
        {
            throw new ArgumentException(
                "Poster target width must be finite and positive.",
                nameof(poster));
        }

        if (poster.TargetHeightMm is double height &&
            (!double.IsFinite(height) || height <= 0))
        {
            throw new ArgumentException(
                "Poster target height must be finite and positive.",
                nameof(poster));
        }

        if (poster.Columns is <= 0 ||
            poster.Rows is <= 0)
        {
            throw new ArgumentException(
                "Poster rows/columns must be positive when provided.",
                nameof(poster));
        }

        if (!double.IsFinite(poster.OverlapMm) ||
            poster.OverlapMm < 0 ||
            !double.IsFinite(poster.MarginMm) ||
            poster.MarginMm < 0)
        {
            throw new ArgumentException(
                "Poster overlap and margin must be finite and non-negative.",
                nameof(poster));
        }
    }

    private sealed record Candidate(
        PageOrientation Orientation,
        double AvailableWidthMm,
        double AvailableHeightMm,
        int Columns,
        int Rows,
        double TargetWidthMm,
        double TargetHeightMm,
        double CoverageWidthMm,
        double CoverageHeightMm,
        int SheetCount,
        double WastedAreaMm2);
}
