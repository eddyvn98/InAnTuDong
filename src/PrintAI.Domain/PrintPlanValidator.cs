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
