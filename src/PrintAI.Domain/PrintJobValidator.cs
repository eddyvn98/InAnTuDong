namespace PrintAI.Domain;

public sealed record ValidationError(string Code, string Message);

public sealed record ValidationResult(IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class PrintJobValidator
{
    public const double MaxA4WidthMm = 210;
    public const double MaxA4HeightMm = 297;

    public static ValidationResult Validate(PrintJobSpec job)
    {
        var errors = new List<ValidationError>();

        if (!string.Equals(job.SchemaVersion, "1.0", StringComparison.Ordinal))
            errors.Add(new("schema.version", "Only PrintJobSpec schemaVersion 1.0 is supported."));

        if (string.IsNullOrWhiteSpace(job.JobName))
            errors.Add(new("job.name", "Job name is required."));

        if (job.Sources is null)
        {
            errors.Add(new("sources.null", "Sources are required."));
            return new(errors);
        }

        if (job.Sources.Count == 0)
            errors.Add(new("sources.empty", "At least one source is required."));

        if (job.Sources.Any(s =>
                !s.IsBlank &&
                string.IsNullOrWhiteSpace(s.Path)))
        {
            errors.Add(new(
                "sources.path",
                "Non-blank source paths cannot be empty."));
        }

        if (job.Sources.Any(s =>
                s.IsBlank &&
                (!string.IsNullOrWhiteSpace(s.Path) ||
                 s.PageIndex != 0)))
        {
            errors.Add(new(
                "sources.blank",
                "Virtual blank sources must not reference a file/page."));
        }

        if (job.Sources.Any(s => s.Copies < 1))
            errors.Add(new("sources.copies", "Source copies must be at least 1."));

        if (job.Sources.Any(s => s.PageIndex < 0))
            errors.Add(new("sources.pageIndex", "Source page index cannot be negative."));

        if (job.Sources.Any(source =>
                (source.OriginalWidthMm is null) !=
                (source.OriginalHeightMm is null)))
        {
            errors.Add(new(
                "sources.physicalSize.pair",
                "Source physical width and height must be provided together."));
        }

        if (job.Sources.Any(source =>
                (source.OriginalWidthMm is double width &&
                 (!double.IsFinite(width) || width <= 0)) ||
                (source.OriginalHeightMm is double height &&
                 (!double.IsFinite(height) || height <= 0))))
        {
            errors.Add(new(
                "sources.physicalSize",
                "Source physical dimensions must be finite and positive when provided."));
        }

        if (job.Paper.WidthMm <= 0 || job.Paper.HeightMm <= 0)
            errors.Add(new("paper.size", "Paper dimensions must be positive."));

        var longSide = Math.Max(job.Paper.WidthMm, job.Paper.HeightMm);
        var shortSide = Math.Min(job.Paper.WidthMm, job.Paper.HeightMm);
        if (shortSide > MaxA4WidthMm || longSide > MaxA4HeightMm)
            errors.Add(new("paper.max", "Paper cannot exceed A4 in the first device profile."));

        if (job.Layout.Mode == LayoutMode.Canvas)
        {
            ValidateCanvas(job, errors);
        }
        else
        {
            if (job.Layout.ItemWidthMm <= 0 || job.Layout.ItemHeightMm <= 0)
                errors.Add(new("layout.itemSize", "Item dimensions must be positive."));

            if (job.Layout.MarginMm < 0 || job.Layout.GapMm < 0)
                errors.Add(new("layout.spacing", "Margin and gap cannot be negative."));
        }

        ValidatePhysicalScale(job, errors);
        ValidatePagePlacement(job, errors);
        ValidateSourceCrop(job, errors);
        ValidatePosterTiles(job, errors);

        if (job.Print.Copies < 1)
            errors.Add(new("print.copies", "Print copies must be at least 1."));

        return new(errors);
    }

    private static void ValidatePosterTiles(
        PrintJobSpec job,
        List<ValidationError> errors)
    {
        var posterSources = job.Sources
            .Where(source => source.PosterTile is not null)
            .ToArray();

        if (job.Layout.Mode != LayoutMode.PosterTile)
        {
            if (posterSources.Length > 0)
            {
                errors.Add(new(
                    "layout.poster.mode",
                    "Poster tile source metadata requires PosterTile layout mode."));
            }

            return;
        }

        if (posterSources.Length != job.Sources.Count)
        {
            errors.Add(new(
                "layout.poster.sources",
                "PosterTile layout requires poster metadata on every source."));
            return;
        }

        if (job.Print.Duplex != DuplexMode.Off)
        {
            errors.Add(new(
                "layout.poster.duplex",
                "Poster tiles must print one-sided."));
        }

        if (job.Layout.PhysicalScale is not null ||
            job.Layout.PagePlacement is not null ||
            job.Layout.SourceCrop is not null ||
            job.Layout.Canvas is not null)
        {
            errors.Add(new(
                "layout.poster.combination",
                "PosterTile layout cannot combine with scaling, placement, general crop or Canvas metadata."));
        }

        foreach (var source in posterSources)
        {
            var tile = source.PosterTile!;

            if (source.Copies != 1)
            {
                errors.Add(new(
                    "layout.poster.copies",
                    "Poster tile sources must use one copy; complete poster sets are separate batches."));
            }

            if (tile.Rows < 1 ||
                tile.Columns < 1 ||
                tile.Row < 0 ||
                tile.Row >= tile.Rows ||
                tile.Column < 0 ||
                tile.Column >= tile.Columns)
            {
                errors.Add(new(
                    "layout.poster.index",
                    "Poster tile row/column metadata is invalid."));
            }

            var dimensions = new[]
            {
                tile.TargetWidthMm,
                tile.TargetHeightMm,
                tile.CanvasXmm,
                tile.CanvasYmm,
                tile.CanvasWidthMm,
                tile.CanvasHeightMm
            };

            if (dimensions.Any(value => !double.IsFinite(value)) ||
                tile.TargetWidthMm <= 0 ||
                tile.TargetHeightMm <= 0 ||
                tile.CanvasXmm < 0 ||
                tile.CanvasYmm < 0 ||
                tile.CanvasWidthMm <= 0 ||
                tile.CanvasHeightMm <= 0 ||
                tile.CanvasXmm + tile.CanvasWidthMm >
                    tile.TargetWidthMm + 0.001 ||
                tile.CanvasYmm + tile.CanvasHeightMm >
                    tile.TargetHeightMm + 0.001)
            {
                errors.Add(new(
                    "layout.poster.geometry",
                    "Poster tile canvas geometry must be finite, positive and inside the poster target."));
            }
        }
    }

    private static void ValidatePagePlacement(
        PrintJobSpec job,
        List<ValidationError> errors)
    {
        var placement = job.Layout.PagePlacement;
        if (placement is null)
            return;

        if (job.Layout.Mode != LayoutMode.ExactSize)
        {
            errors.Add(new(
                "layout.pagePlacement.mode",
                "Page placement is currently supported only for ExactSize layouts."));
        }

        var margins = placement.Margins;
        var values = new[]
        {
            margins.LeftMm,
            margins.TopMm,
            margins.RightMm,
            margins.BottomMm
        };

        if (values.Any(value =>
                !double.IsFinite(value) || value < 0))
        {
            errors.Add(new(
                "layout.pagePlacement.margins",
                "Page margins must be finite and non-negative."));
        }

        if (!double.IsFinite(placement.OffsetXMm) ||
            !double.IsFinite(placement.OffsetYMm))
        {
            errors.Add(new(
                "layout.pagePlacement.offset",
                "Page placement offsets must be finite."));
        }

        var paperWidth =
            job.Paper.Orientation == PageOrientation.Portrait
                ? job.Paper.WidthMm
                : job.Paper.HeightMm;
        var paperHeight =
            job.Paper.Orientation == PageOrientation.Portrait
                ? job.Paper.HeightMm
                : job.Paper.WidthMm;

        if (margins.LeftMm + margins.RightMm >= paperWidth ||
            margins.TopMm + margins.BottomMm >= paperHeight)
        {
            errors.Add(new(
                "layout.pagePlacement.area",
                "Page margins must leave a positive printable placement area."));
        }
    }

    private static void ValidateSourceCrop(
        PrintJobSpec job,
        List<ValidationError> errors)
    {
        var crop = job.Layout.SourceCrop;
        if (crop is null)
            return;

        if (job.Layout.Mode == LayoutMode.Canvas)
        {
            errors.Add(new(
                "layout.sourceCrop.canvas",
                "General source crop is not supported on Canvas layouts."));
        }

        if (job.Layout.PhysicalScale is not null)
        {
            errors.Add(new(
                "layout.sourceCrop.scaling",
                "General source crop cannot be combined with physical scaling in the current slice."));
        }

        if (crop.Mode != SourceCropMode.EdgesMm)
            return;

        var edges = crop.EdgesMm;
        if (edges is null)
        {
            errors.Add(new(
                "layout.sourceCrop.edges",
                "EdgesMm crop requires explicit edge values."));
            return;
        }

        var edgeValues = new[]
        {
            edges.LeftMm,
            edges.TopMm,
            edges.RightMm,
            edges.BottomMm
        };

        if (edgeValues.Any(value =>
                !double.IsFinite(value) || value < 0))
        {
            errors.Add(new(
                "layout.sourceCrop.edges",
                "Crop edge values must be finite and non-negative."));
            return;
        }

        foreach (var source in job.Sources)
        {
            if (source.OriginalWidthMm is not double width ||
                source.OriginalHeightMm is not double height)
            {
                errors.Add(new(
                    "layout.sourceCrop.sourceSize",
                    "Millimetre edge crop requires trusted source physical dimensions."));
                continue;
            }

            if (edges.LeftMm + edges.RightMm >= width ||
                edges.TopMm + edges.BottomMm >= height)
            {
                errors.Add(new(
                    "layout.sourceCrop.bounds",
                    "Crop edges must leave a positive source area."));
            }
        }
    }

    private static void ValidatePhysicalScale(
        PrintJobSpec job,
        List<ValidationError> errors)
    {
        var scaling = job.Layout.PhysicalScale;
        if (scaling is null)
            return;

        if (job.Layout.Mode == LayoutMode.Canvas)
        {
            errors.Add(new(
                "layout.physicalScale.canvas",
                "Physical scaling is not supported on Canvas layouts."));
        }

        if (job.Layout.Fit != FitMode.Contain)
        {
            errors.Add(new(
                "layout.physicalScale.fit",
                "Physical scaling requires Contain fit; Cover is a separate crop/fill intent."));
        }

        if (scaling.Mode == PhysicalScaleMode.Percent &&
            (!double.IsFinite(scaling.Percent) ||
             scaling.Percent <= 0 ||
             scaling.Percent > 1000))
        {
            errors.Add(new(
                "layout.physicalScale.percent",
                "Physical scale percent must be greater than 0 and at most 1000."));
        }

        if (scaling.Mode is
                PhysicalScaleMode.ShrinkOnly or
                PhysicalScaleMode.Percent)
        {
            if (job.Sources.Any(source =>
                    source.OriginalWidthMm is null ||
                    source.OriginalHeightMm is null ||
                    source.OriginalWidthMm <= 0 ||
                    source.OriginalHeightMm <= 0))
            {
                errors.Add(new(
                    "layout.physicalScale.sourceSize",
                    "Shrink-only and percent scaling require trusted source physical dimensions."));
            }
        }
    }

    private static void ValidateCanvas(
        PrintJobSpec job,
        List<ValidationError> errors)
    {
        var canvas = job.Layout.Canvas;
        if (canvas is null || canvas.Placements.Count == 0)
        {
            errors.Add(new("layout.canvas.empty", "Canvas layout requires at least one placement."));
            return;
        }

        var paperWidth = job.Paper.Orientation == PageOrientation.Portrait
            ? job.Paper.WidthMm
            : job.Paper.HeightMm;
        var paperHeight = job.Paper.Orientation == PageOrientation.Portrait
            ? job.Paper.HeightMm
            : job.Paper.WidthMm;

        foreach (var placement in canvas.Placements)
        {
            if (placement.SourceIndex < 0 || placement.SourceIndex >= job.Sources.Count)
                errors.Add(new("layout.canvas.source", "Canvas placement references an unavailable source."));

            if (placement.WidthMm <= 0 || placement.HeightMm <= 0)
                errors.Add(new("layout.canvas.size", "Canvas placement dimensions must be positive."));

            if (placement.XMm < 0 || placement.YMm < 0 ||
                placement.XMm + placement.WidthMm > paperWidth + 0.01 ||
                placement.YMm + placement.HeightMm > paperHeight + 0.01)
            {
                errors.Add(new("layout.canvas.bounds", "Canvas placement must remain inside the paper."));
            }

            var transform = placement.Transform ?? new ImageTransformSpec();
            if (transform.Scale <= 0 || transform.Scale > 10)
                errors.Add(new("layout.canvas.scale", "Canvas image scale must be greater than 0 and at most 10."));

            if (transform.OffsetX < -1 || transform.OffsetX > 1 ||
                transform.OffsetY < -1 || transform.OffsetY > 1)
            {
                errors.Add(new("layout.canvas.offset", "Canvas image offsets must be between -1 and 1."));
            }

            var shape = placement.Shape ?? new ShapeSpec();
            if (shape.CornerRadiusMm < 0)
                errors.Add(new("layout.canvas.cornerRadius", "Canvas corner radius cannot be negative."));
        }
    }
}
