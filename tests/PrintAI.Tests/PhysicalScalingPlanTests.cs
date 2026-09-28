using PrintAI.Domain;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class PhysicalScalingPlanTests
{
    [Fact]
    public void PercentScaling_CompilesTrustedPageSizeIntoSourceSpec()
    {
        var plan = Plan(
            new PhysicalScaleSpec(
                PhysicalScaleMode.Percent,
                Percent: 80),
            pages:
            [
                new SourcePageSizeSpec(0, 210, 297)
            ]);

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        var source = Assert.Single(job.Sources);
        Assert.Equal(210, source.OriginalWidthMm);
        Assert.Equal(297, source.OriginalHeightMm);
        Assert.NotNull(job.Layout.PhysicalScale);
        Assert.Equal(
            PhysicalScaleMode.Percent,
            job.Layout.PhysicalScale!.Mode);
        Assert.Equal(80, job.Layout.PhysicalScale.Percent);
    }

    [Fact]
    public void ShrinkOnly_PreservesDifferentPhysicalPageSizesInOneJob()
    {
        var plan = new PrintPlan(
            "mixed sizes",
            [
                new PlanSourceSpec(
                    "C:/print/doc.pdf",
                    2,
                    [
                        new SourcePageSizeSpec(0, 148, 210),
                        new SourcePageSizeSpec(1, 210, 297)
                    ])
            ],
            [
                Group(
                    new PhysicalScaleSpec(
                        PhysicalScaleMode.ShrinkOnly),
                    pageCount: 2)
            ],
            new PolicySpec());

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(148, job.Sources[0].OriginalWidthMm);
        Assert.Equal(210, job.Sources[0].OriginalHeightMm);
        Assert.Equal(210, job.Sources[1].OriginalWidthMm);
        Assert.Equal(297, job.Sources[1].OriginalHeightMm);
        Assert.Equal(
            2,
            SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void PercentScaling_RejectsMissingPhysicalPageMetadata()
    {
        var plan = Plan(
            new PhysicalScaleSpec(
                PhysicalScaleMode.Percent,
                Percent: 80),
            pages: null);

        var error = Assert.Throws<ArgumentException>(() =>
            PrintPlanCompiler.Compile(plan));

        Assert.Contains(
            "trusted page size metadata",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScalingAndNUp_AreRejectedAsAmbiguousInCurrentSlice()
    {
        var group = Group(
            new PhysicalScaleSpec(
                PhysicalScaleMode.ShrinkOnly),
            pageCount: 4) with
        {
            NUp = new NUpSpec(4)
        };

        var plan = new PrintPlan(
            "invalid",
            [
                new PlanSourceSpec(
                    "C:/print/doc.pdf",
                    4,
                    Enumerable.Range(0, 4)
                        .Select(index =>
                            new SourcePageSizeSpec(
                                index,
                                210,
                                297))
                        .ToArray())
            ],
            [group],
            new PolicySpec());

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error => error.Code == "plan.groups.scaling.nup");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void PercentScaling_RejectsInvalidPercent(
        double percent)
    {
        var plan = Plan(
            new PhysicalScaleSpec(
                PhysicalScaleMode.Percent,
                percent),
            pages:
            [
                new SourcePageSizeSpec(0, 210, 297)
            ]);

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error => error.Code ==
                "plan.groups.scaling.percent");
    }

    private static PrintPlan Plan(
        PhysicalScaleSpec scaling,
        IReadOnlyList<SourcePageSizeSpec>? pages) =>
        new(
            "scale",
            [new PlanSourceSpec("C:/print/doc.pdf", 1, pages)],
            [Group(scaling, pageCount: 1)],
            new PolicySpec());

    private static PrintOutputGroupSpec Group(
        PhysicalScaleSpec scaling,
        int pageCount) =>
        new(
            Name: "scaled",
            Selections:
            [
                new PageSelectionSpec(
                    0,
                    [new PageRangeSpec(1, pageCount)])
            ],
            Paper: new PaperSpec(),
            Layout: new LayoutSpec(
                LayoutMode.ExactSize,
                200,
                287,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            Print: new OutputPrintSettings(),
            Scaling: scaling);
}
