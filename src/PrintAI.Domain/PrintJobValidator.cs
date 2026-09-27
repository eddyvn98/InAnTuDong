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

        if (job.Paper.WidthMm <= 0 || job.Paper.HeightMm <= 0)
            errors.Add(new("paper.size", "Paper dimensions must be positive."));

        var longSide = Math.Max(job.Paper.WidthMm, job.Paper.HeightMm);
        var shortSide = Math.Min(job.Paper.WidthMm, job.Paper.HeightMm);
        if (shortSide > MaxA4WidthMm || longSide > MaxA4HeightMm)
            errors.Add(new("paper.max", "Paper cannot exceed A4 in the first device profile."));

        if (job.Layout.ItemWidthMm <= 0 || job.Layout.ItemHeightMm <= 0)
            errors.Add(new("layout.itemSize", "Item dimensions must be positive."));

        if (job.Layout.MarginMm < 0 || job.Layout.GapMm < 0)
            errors.Add(new("layout.spacing", "Margin and gap cannot be negative."));

        if (job.Print.Copies < 1)
            errors.Add(new("print.copies", "Print copies must be at least 1."));

        return new(errors);
    }
}
