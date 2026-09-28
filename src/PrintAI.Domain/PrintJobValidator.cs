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

        if (job.Sources.Any(s => string.IsNullOrWhiteSpace(s.Path)))
            errors.Add(new("sources.path", "Source paths cannot be empty."));

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

        if (job.Print.Copies < 1)
            errors.Add(new("print.copies", "Print copies must be at least 1."));

        return new(errors);
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
