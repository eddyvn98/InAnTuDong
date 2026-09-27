using PrintAI.Domain;

namespace PrintAI.Planning;

public static class PlannerSourceBinder
{
    public static PrintJobSpec BindToAllowedSources(
        PrintJobSpec proposal,
        IReadOnlyCollection<string> allowedPaths)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(allowedPaths);

        if (allowedPaths.Count == 0)
            throw new PlanningFormatException("No allowed source path is available.");

        var allowed = new HashSet<string>(
            allowedPaths.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in proposal.Sources)
        {
            var fullPath = Path.GetFullPath(source.Path);
            if (!allowed.Contains(fullPath))
            {
                throw new PlanningFormatException(
                    $"Planner referenced an unapproved source path: {source.Path}");
            }
        }

        return proposal with
        {
            Sources = proposal.Sources
                .Select(source => source with { Path = Path.GetFullPath(source.Path) })
                .ToArray()
        };
    }
}
