using PrintAI.Domain;

namespace PrintAI.Planning;

public static class GeneralPrintPlanSourceBinder
{
    public static PrintPlan BindToAllowedSources(
        PrintPlan proposal,
        IReadOnlyList<PlanningSource> allowedSources)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(allowedSources);

        if (allowedSources.Count == 0)
            throw new PlanningFormatException("No allowed source path is available.");

        var allowed = allowedSources
            .Select(source => new
            {
                Source = source,
                FullPath = Path.GetFullPath(source.Path)
            })
            .ToDictionary(
                item => item.FullPath,
                item => item.Source,
                StringComparer.OrdinalIgnoreCase);

        var boundSources = new PlanSourceSpec[proposal.Sources.Count];

        for (var index = 0; index < proposal.Sources.Count; index++)
        {
            var source = proposal.Sources[index];
            var fullPath = Path.GetFullPath(source.Path);

            if (!allowed.TryGetValue(fullPath, out var approved))
            {
                throw new PlanningFormatException(
                    $"Planner referenced an unapproved source path: {source.Path}");
            }

            var approvedPageCount = approved.PageCount ?? 1;
            if (source.PageCount != approvedPageCount)
            {
                throw new PlanningFormatException(
                    $"Planner changed pageCount for source {source.Path}: " +
                    $"expected {approvedPageCount}, got {source.PageCount}.");
            }

            var approvedPages = approved.Pages;
            if (source.Pages is { Count: > 0 } &&
                !PageSizesMatch(source.Pages, approvedPages))
            {
                throw new PlanningFormatException(
                    $"Planner changed physical page sizes for source {source.Path}.");
            }

            boundSources[index] = source with
            {
                Path = fullPath,
                Pages = approvedPages
            };
        }

        return proposal with { Sources = boundSources };
    }

    private static bool PageSizesMatch(
        IReadOnlyList<SourcePageSizeSpec> proposed,
        IReadOnlyList<SourcePageSizeSpec>? approved)
    {
        if (approved is null || proposed.Count != approved.Count)
            return false;

        for (var index = 0; index < proposed.Count; index++)
        {
            var left = proposed[index];
            var right = approved[index];

            if (left.PageIndex != right.PageIndex ||
                Math.Abs(left.WidthMm - right.WidthMm) > 0.01 ||
                Math.Abs(left.HeightMm - right.HeightMm) > 0.01)
            {
                return false;
            }
        }

        return true;
    }
}
