using PrintAI.Domain;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class GeneralPrintPlanSourceBinderTests
{
    [Fact]
    public void BindToAllowedSources_NormalizesApprovedPaths()
    {
        var relativePath = Path.Combine(".", "doc.pdf");
        var plan = Plan(relativePath, pageCount: 5);

        var bound = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            plan,
            [new(relativePath, "Pdf", PageCount: 5)]);

        Assert.Equal(Path.GetFullPath(relativePath), bound.Sources[0].Path);
    }

    [Fact]
    public void BindToAllowedSources_RejectsChangedPageCount()
    {
        var plan = Plan("C:/print/doc.pdf", pageCount: 99);

        var error = Assert.Throws<PlanningFormatException>(() =>
            GeneralPrintPlanSourceBinder.BindToAllowedSources(
                plan,
                [new("C:/print/doc.pdf", "Pdf", PageCount: 5)]));

        Assert.Contains("pageCount", error.Message);
    }

    private static PrintPlan Plan(string path, int pageCount) =>
        new(
            "Plan",
            [new(path, pageCount)],
            [
                new(
                    "All",
                    [new(0)],
                    new(),
                    new(
                        LayoutMode.ExactSize,
                        ItemWidthMm: 200,
                        ItemHeightMm: 287),
                    new())
            ],
            new());
}
