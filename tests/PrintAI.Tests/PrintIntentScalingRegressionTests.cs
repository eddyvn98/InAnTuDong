using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentScalingRegressionTests
{
    [Fact]
    public void Scale003_ShrinkOnly_DoesNotUpscaleSmallerPages()
    {
        var plan = Plan(
            new PhysicalScaleSpec(
                PhysicalScaleMode.ShrinkOnly),
            [
                new SourcePageSizeSpec(0, 148, 210)
            ]);

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        var geometry = PhysicalScaleCalculator.Calculate(
            sourcePixelWidth: 1480,
            sourcePixelHeight: 2100,
            targetWidthPx: 2000,
            targetHeightPx: 2870,
            targetWidthMm: 200,
            targetHeightMm: 287,
            sourceWidthMm: job.Sources[0].OriginalWidthMm,
            sourceHeightMm: job.Sources[0].OriginalHeightMm,
            scaling: job.Layout.PhysicalScale!);

        Assert.True(geometry.Destination.Width < 1);
        Assert.True(geometry.Destination.Height < 1);
    }

    [Fact]
    public void Scale004_EightyPercent_CompilesExactPercentIntent()
    {
        var job = Assert.Single(
            PrintPlanCompiler.Compile(
                Plan(
                    new PhysicalScaleSpec(
                        PhysicalScaleMode.Percent,
                        Percent: 80),
                    [new SourcePageSizeSpec(0, 210, 297)]))
            .Batches).Job;

        Assert.Equal(
            PhysicalScaleMode.Percent,
            job.Layout.PhysicalScale!.Mode);
        Assert.Equal(80, job.Layout.PhysicalScale.Percent);
    }

    [Fact]
    public void Scale005_OneHundredTwentyFivePercent_IsPreserved()
    {
        var job = Assert.Single(
            PrintPlanCompiler.Compile(
                Plan(
                    new PhysicalScaleSpec(
                        PhysicalScaleMode.Percent,
                        Percent: 125),
                    [new SourcePageSizeSpec(0, 210, 297)]))
            .Batches).Job;

        Assert.Equal(125, job.Layout.PhysicalScale!.Percent);
    }

    [Fact]
    public void Scale008_MixedPhysicalPages_KeepIndependentOriginalSizes()
    {
        var plan = new PrintPlan(
            "mixed physical pages",
            [
                new PlanSourceSpec(
                    "C:/print/doc.pdf",
                    2,
                    [
                        new SourcePageSizeSpec(0, 148, 210),
                        new SourcePageSizeSpec(1, 297, 420)
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
        Assert.Equal(297, job.Sources[1].OriginalWidthMm);
        Assert.Equal(420, job.Sources[1].OriginalHeightMm);
    }

    [Fact]
    public void Scale009_OneToTwoRatio_MapsToFiftyPercent()
    {
        var job = Assert.Single(
            PrintPlanCompiler.Compile(
                Plan(
                    new PhysicalScaleSpec(
                        PhysicalScaleMode.Percent,
                        Percent: 50),
                    [new SourcePageSizeSpec(0, 210, 297)]))
            .Batches).Job;

        Assert.Equal(50, job.Layout.PhysicalScale!.Percent);
    }

    [Fact]
    public void Scale010_MaxFit_DoesNotRequirePhysicalSourceMetadata()
    {
        var plan = Plan(
            new PhysicalScaleSpec(
                PhysicalScaleMode.MaxFit),
            pages: null);

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(
            PhysicalScaleMode.MaxFit,
            job.Layout.PhysicalScale!.Mode);
        Assert.Equal(FitMode.Contain, job.Layout.Fit);
    }

    private static PrintPlan Plan(
        PhysicalScaleSpec scaling,
        IReadOnlyList<SourcePageSizeSpec>? pages) =>
        new(
            "scaling regression",
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
