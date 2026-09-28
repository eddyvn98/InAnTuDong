namespace PrintAI.Domain;

public static class PrintPlanCompiler
{
    public static CompiledPrintPlan Compile(PrintPlan plan)
    {
        var validation = PrintPlanValidator.Validate(plan);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                string.Join("; ", validation.Errors.Select(error => error.Message)),
                nameof(plan));
        }

        var batches = new List<CompiledPrintBatch>();
        var orderedGroups = plan.OutputGroups
            .Select((group, index) => new { Group = group, Index = index })
            .OrderBy(item => item.Group.Sequence)
            .ToArray();

        var batchSequence = 0;

        if (orderedGroups.Length > 1 &&
            orderedGroups.All(item => item.Group.Collate))
        {
            var setCount = orderedGroups[0].Group.Sets;

            for (var setNumber = 1; setNumber <= setCount; setNumber++)
            {
                foreach (var item in orderedGroups)
                {
                    var expandedSources = ExpandSources(plan, item.Group);
                    var job = CreateJob(
                        plan,
                        item.Group,
                        expandedSources,
                        copiesPerSource: 1,
                        setNumber);

                    ValidateCompiledJob(job);
                    batches.Add(new(batchSequence++, item.Index, setNumber, job));
                }
            }
        }
        else
        {
            foreach (var item in orderedGroups)
            {
                var expandedSources = ExpandSources(plan, item.Group);

                if (item.Group.Collate)
                {
                    for (var setNumber = 1; setNumber <= item.Group.Sets; setNumber++)
                    {
                        var job = CreateJob(
                            plan,
                            item.Group,
                            expandedSources,
                            copiesPerSource: 1,
                            setNumber);

                        ValidateCompiledJob(job);
                        batches.Add(new(batchSequence++, item.Index, setNumber, job));
                    }
                }
                else
                {
                    var job = CreateJob(
                        plan,
                        item.Group,
                        expandedSources,
                        copiesPerSource: item.Group.Sets,
                        setNumber: 0);

                    ValidateCompiledJob(job);
                    batches.Add(new(batchSequence++, item.Index, 0, job));
                }
            }
        }

        return new(plan.PlanName, batches);
    }

    private static IReadOnlyList<SourceSpec> ExpandSources(
        PrintPlan plan,
        PrintOutputGroupSpec group)
    {
        var sources = new List<SourceSpec>();

        foreach (var selection in group.Selections)
        {
            var planSource = plan.Sources[selection.SourceIndex];
            foreach (var page in PrintPlanValidator.ResolvePages(plan, selection))
            {
                var pageIndex = page - 1;
                var physical = planSource.Pages?
                    .FirstOrDefault(size => size.PageIndex == pageIndex);

                sources.Add(new(
                    Path: planSource.Path,
                    Copies: 1,
                    PageIndex: pageIndex,
                    OriginalWidthMm: physical?.WidthMm,
                    OriginalHeightMm: physical?.HeightMm));
            }
        }

        return sources;
    }

    private static PrintJobSpec CreateJob(
        PrintPlan plan,
        PrintOutputGroupSpec group,
        IReadOnlyList<SourceSpec> expandedSources,
        int copiesPerSource,
        int setNumber)
    {
        var sources = expandedSources
            .Select(source => source with { Copies = copiesPerSource })
            .ToArray();

        var jobName = setNumber > 0 && group.Sets > 1
            ? $"{group.Name} - set {setNumber}/{group.Sets}"
            : group.Name;

        var (paper, resolvedLayout) =
            NUpLayoutResolver.Resolve(group);

        var layout = resolvedLayout with
        {
            PhysicalScale = group.Scaling
        };

        return new(
            JobName: jobName,
            Sources: sources,
            Paper: paper,
            Layout: layout,
            Print: new(
                Copies: 1,
                ColorMode: group.Print.ColorMode,
                Quality: group.Print.Quality,
                Duplex: group.Print.Duplex),
            Policy: plan.Policy,
            SchemaVersion: "1.0");
    }

    private static void ValidateCompiledJob(PrintJobSpec job)
    {
        var validation = PrintJobValidator.Validate(job);
        if (validation.IsValid)
            return;

        var details = string.Join(
            "; ",
            validation.Errors.Select(error => $"{error.Code}: {error.Message}"));

        throw new InvalidOperationException(
            $"Compiled PrintJobSpec is invalid: {details}");
    }
}
