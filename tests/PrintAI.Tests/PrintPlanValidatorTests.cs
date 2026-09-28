using PrintAI.Domain;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintPlanValidatorTests
{
    [Fact]
    public void ResolvePages_SupportsRangesExclusionsAndParity()
    {
        var plan = new PrintPlan(
            "Selection",
            [new("C:/print/doc.pdf", PageCount: 10)],
            [
                new(
                    "Odd except 3",
                    [new(
                        SourceIndex: 0,
                        Include: [new(1, 10)],
                        Exclude: [new(3, 3)],
                        Parity: PageParity.Odd)],
                    new(),
                    new(
                        LayoutMode.ExactSize,
                        ItemWidthMm: 200,
                        ItemHeightMm: 287),
                    new())
            ],
            new());

        var pages = PrintPlanValidator.ResolvePages(
            plan,
            plan.OutputGroups[0].Selections[0]);

        Assert.Equal([1, 5, 7, 9], pages);
        Assert.True(PrintPlanValidator.Validate(plan).IsValid);
    }

    [Fact]
    public void Validate_RejectsOutOfRangePagesAndDuplicateSequence()
    {
        var group = new PrintOutputGroupSpec(
            "Bad",
            [new(0, [new(1, 6)])],
            new(),
            new(
                LayoutMode.ExactSize,
                ItemWidthMm: 200,
                ItemHeightMm: 287),
            new(),
            Sequence: 0);

        var plan = new PrintPlan(
            "Bad plan",
            [new("C:/print/doc.pdf", PageCount: 5)],
            [group, group with { Name = "Also bad" }],
            new());

        var result = PrintPlanValidator.Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "plan.selection.range");
        Assert.Contains(result.Errors, error => error.Code == "plan.groups.sequence");
    }
}
