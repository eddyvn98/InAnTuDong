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
    public void BindToAllowedSources_InjectsTrustedPhysicalPageSizes()
    {
        var plan = Plan("C:/print/doc.pdf", pageCount: 2);

        var bound = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            plan,
            [
                new PlanningSource(
                    "C:/print/doc.pdf",
                    "Pdf",
                    PageCount: 2,
                    Pages:
                    [
                        new SourcePageSizeSpec(0, 210, 297),
                        new SourcePageSizeSpec(1, 148, 210)
                    ])
            ]);

        var pages = Assert.IsAssignableFrom<
            IReadOnlyList<SourcePageSizeSpec>>(
                bound.Sources[0].Pages);

        Assert.Equal(2, pages.Count);
        Assert.Equal(210, pages[0].WidthMm);
        Assert.Equal(148, pages[1].WidthMm);
    }

    [Fact]
    public void BindToAllowedSources_RejectsPlannerRewrittenPhysicalPageSizes()
    {
        var plan = Plan("C:/print/doc.pdf", pageCount: 1) with
        {
            Sources =
            [
                new PlanSourceSpec(
                    "C:/print/doc.pdf",
                    1,
                    [new SourcePageSizeSpec(0, 999, 999)])
            ]
        };

        var error = Assert.Throws<PlanningFormatException>(() =>
            GeneralPrintPlanSourceBinder.BindToAllowedSources(
                plan,
                [
                    new PlanningSource(
                        "C:/print/doc.pdf",
                        "Pdf",
                        PageCount: 1,
                        Pages:
                        [
                            new SourcePageSizeSpec(0, 210, 297)
                        ])
                ]));

        Assert.Contains(
            "physical page sizes",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindToAllowedSources_InjectsTrustedPixelAspect()
    {
        var plan = Plan("C:/print/image.png", pageCount: 1);

        var bound = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            plan,
            [
                new PlanningSource(
                    "C:/print/image.png",
                    "Raster",
                    PixelWidth: 4000,
                    PixelHeight: 2000,
                    PageCount: 1)
            ]);

        Assert.Equal(4000, bound.Sources[0].PixelWidth);
        Assert.Equal(2000, bound.Sources[0].PixelHeight);
    }

    [Fact]
    public void BindToAllowedSources_RejectsPlannerRewrittenPixelAspect()
    {
        var plan = Plan("C:/print/image.png", pageCount: 1) with
        {
            Sources =
            [
                new PlanSourceSpec(
                    "C:/print/image.png",
                    1,
                    PixelWidth: 999,
                    PixelHeight: 999)
            ]
        };

        var error = Assert.Throws<PlanningFormatException>(() =>
            GeneralPrintPlanSourceBinder.BindToAllowedSources(
                plan,
                [
                    new PlanningSource(
                        "C:/print/image.png",
                        "Raster",
                        PixelWidth: 4000,
                        PixelHeight: 2000,
                        PageCount: 1)
                ]));

        Assert.Contains(
            "pixel width",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
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
