namespace PrintAI.Domain;

public static class PrintPlanValidator
{
    public static ValidationResult Validate(PrintPlan plan)
    {
        var errors = new List<ValidationError>();

        if (!string.Equals(plan.SchemaVersion, "2.0", StringComparison.Ordinal))
            errors.Add(new("plan.schema.version", "Only PrintPlan schemaVersion 2.0 is supported."));

        if (string.IsNullOrWhiteSpace(plan.PlanName))
            errors.Add(new("plan.name", "Plan name is required."));

        if (plan.Sources is null || plan.Sources.Count == 0)
        {
            errors.Add(new("plan.sources.empty", "At least one plan source is required."));
            return new(errors);
        }

        var duplicatePaths = plan.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Path))
            .GroupBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicatePaths.Length > 0)
        {
            errors.Add(new(
                "plan.sources.duplicate",
                "Each approved source path may appear only once in PrintPlan sources."));
        }

        for (var sourceIndex = 0; sourceIndex < plan.Sources.Count; sourceIndex++)
        {
            var source = plan.Sources[sourceIndex];
            if (string.IsNullOrWhiteSpace(source.Path))
                errors.Add(new("plan.sources.path", $"Source {sourceIndex} path cannot be empty."));

            if (source.PageCount < 1)
                errors.Add(new("plan.sources.pageCount", $"Source {sourceIndex} page count must be at least 1."));
        }

        if (plan.OutputGroups is null || plan.OutputGroups.Count == 0)
        {
            errors.Add(new("plan.groups.empty", "At least one output group is required."));
            return new(errors);
        }

        var duplicateSequences = plan.OutputGroups
            .GroupBy(group => group.Sequence)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateSequences.Length > 0)
        {
            errors.Add(new(
                "plan.groups.sequence",
                $"Output group sequence values must be unique: {string.Join(", ", duplicateSequences)}."));
        }

        for (var groupIndex = 0; groupIndex < plan.OutputGroups.Count; groupIndex++)
            ValidateGroup(plan, groupIndex, errors);

        ValidateCrossGroupSetSemantics(plan, errors);

        return new(errors);
    }

    public static IReadOnlyList<int> ResolvePages(
        PrintPlan plan,
        PageSelectionSpec selection)
    {
        if (selection.SourceIndex < 0 || selection.SourceIndex >= plan.Sources.Count)
            return [];

        var pageCount = plan.Sources[selection.SourceIndex].PageCount;
        var pages = new List<int>();
        var seen = new HashSet<int>();

        var include = selection.Include;
        if (include is null || include.Count == 0)
        {
            for (var page = 1; page <= pageCount; page++)
                AddIfNew(page);
        }
        else
        {
            foreach (var range in include)
            {
                for (var page = range.StartPage; page <= range.EndPage; page++)
                    AddIfNew(page);
            }
        }

        if (selection.Exclude is { Count: > 0 })
        {
            var excluded = new HashSet<int>();
            foreach (var range in selection.Exclude)
            {
                for (var page = range.StartPage; page <= range.EndPage; page++)
                    excluded.Add(page);
            }

            pages.RemoveAll(excluded.Contains);
        }

        pages.RemoveAll(page => selection.Parity switch
        {
            PageParity.Odd => page % 2 == 0,
            PageParity.Even => page % 2 != 0,
            _ => false
        });

        return pages;

        void AddIfNew(int page)
        {
            if (seen.Add(page))
                pages.Add(page);
        }
    }

    private static void ValidateGroup(
        PrintPlan plan,
        int groupIndex,
        List<ValidationError> errors)
    {
        var group = plan.OutputGroups[groupIndex];
        var prefix = $"Output group {groupIndex}";

        if (string.IsNullOrWhiteSpace(group.Name))
            errors.Add(new("plan.groups.name", $"{prefix} name is required."));

        if (group.Sets < 1)
            errors.Add(new("plan.groups.sets", $"{prefix} sets must be at least 1."));

        if (group.NUp is not null)
        {
            ValidateNUp(group, prefix, errors);
        }
        else if (group.Layout.Mode == LayoutMode.Canvas)
        {
            errors.Add(new(
                "plan.groups.canvas",
                $"{prefix} cannot use Canvas layout in PrintPlan 2.0 foundation; use the dedicated Smart Collage workflow."));
        }

        if (group.Selections is null || group.Selections.Count == 0)
        {
            errors.Add(new("plan.groups.selections", $"{prefix} requires at least one page selection."));
            return;
        }

        foreach (var selection in group.Selections)
        {
            if (selection.SourceIndex < 0 || selection.SourceIndex >= plan.Sources.Count)
            {
                errors.Add(new(
                    "plan.selection.source",
                    $"{prefix} references unavailable source index {selection.SourceIndex}."));
                continue;
            }

            var pageCount = plan.Sources[selection.SourceIndex].PageCount;
            ValidateRanges(selection.Include, pageCount, "include", prefix, errors);
            ValidateRanges(selection.Exclude, pageCount, "exclude", prefix, errors);

            if (ResolvePages(plan, selection).Count == 0)
            {
                errors.Add(new(
                    "plan.selection.empty",
                    $"{prefix} resolves to zero source pages."));
            }
        }

        if (!errors.Any(error =>
                error.Code.StartsWith("plan.selection", StringComparison.Ordinal) ||
                error.Code == "plan.groups.canvas" ||
                error.Code.StartsWith("plan.groups.nup", StringComparison.Ordinal)))
        {
            var firstSelection = group.Selections[0];
            var firstPage = ResolvePages(plan, firstSelection)[0];
            var (paper, layout) = NUpLayoutResolver.Resolve(group);
            var representative = new PrintJobSpec(
                JobName: group.Name,
                Sources:
                [
                    new SourceSpec(
                        plan.Sources[firstSelection.SourceIndex].Path,
                        PageIndex: firstPage - 1)
                ],
                Paper: paper,
                Layout: layout,
                Print: new PrintSettings(
                    Copies: 1,
                    ColorMode: group.Print.ColorMode,
                    Quality: group.Print.Quality,
                    Duplex: group.Print.Duplex),
                Policy: plan.Policy,
                SchemaVersion: "1.0");

            var jobValidation = PrintJobValidator.Validate(representative);
            foreach (var error in jobValidation.Errors)
            {
                errors.Add(new(
                    $"plan.group.job.{error.Code}",
                    $"{prefix}: {error.Message}"));
            }
        }
    }

    private static void ValidateNUp(
        PrintOutputGroupSpec group,
        string prefix,
        List<ValidationError> errors)
    {
        var nUp = group.NUp!;

        if (!NUpLayoutResolver.SupportedPagesPerSheet.Contains(
                nUp.PagesPerSheet))
        {
            errors.Add(new(
                "plan.groups.nup.pagesPerSheet",
                $"{prefix} pagesPerSheet must be one of: " +
                $"{string.Join(", ", NUpLayoutResolver.SupportedPagesPerSheet.Order())}."));
        }

        if (nUp.GapMm < 0 || nUp.MarginMm < 0)
        {
            errors.Add(new(
                "plan.groups.nup.spacing",
                $"{prefix} N-up gap and margin cannot be negative."));
        }

        if (errors.Any(error =>
                error.Code.StartsWith("plan.groups.nup", StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            _ = NUpLayoutResolver.Resolve(group);
        }
        catch (ArgumentException ex)
        {
            errors.Add(new(
                "plan.groups.nup.geometry",
                $"{prefix}: {ex.Message}"));
        }
    }

    private static void ValidateCrossGroupSetSemantics(
        PrintPlan plan,
        List<ValidationError> errors)
    {
        if (plan.OutputGroups.Count <= 1)
            return;

        var collatedGroups = plan.OutputGroups
            .Where(group => group.Collate)
            .ToArray();

        if (collatedGroups.Length == 0)
            return;

        if (collatedGroups.Length != plan.OutputGroups.Count)
        {
            errors.Add(new(
                "plan.groups.collation",
                "Multi-group plans cannot mix collated and non-collated groups because complete-set ordering would be ambiguous."));
            return;
        }

        var setCounts = collatedGroups
            .Select(group => group.Sets)
            .Distinct()
            .ToArray();

        if (setCounts.Length != 1)
        {
            errors.Add(new(
                "plan.groups.sets",
                "All collated groups in one plan must use the same sets value so complete sets can be interleaved correctly."));
        }
    }

    private static void ValidateRanges(
        IReadOnlyList<PageRangeSpec>? ranges,
        int pageCount,
        string kind,
        string prefix,
        List<ValidationError> errors)
    {
        if (ranges is null)
            return;

        foreach (var range in ranges)
        {
            if (range.StartPage < 1 ||
                range.EndPage < range.StartPage ||
                range.EndPage > pageCount)
            {
                errors.Add(new(
                    "plan.selection.range",
                    $"{prefix} {kind} range {range.StartPage}-{range.EndPage} is outside 1-{pageCount}."));
            }
        }
    }
}
