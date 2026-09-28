using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class VariableItemsResolverTests
{
    [Fact]
    public void DifferentSizes_PackOnSameA4()
    {
        var job = Compile(
            [
                new VariableItemSpec(0, WidthMm: 30, HeightMm: 40),
                new VariableItemSpec(1, WidthMm: 40, HeightMm: 60)
            ]);

        var layout = CanvasLayoutEngine.Layout(job);

        Assert.Equal(2, layout.Placements.Count);
        Assert.Single(layout.Placements.Select(p => p.Page).Distinct());
        Assert.Contains(layout.Placements, p => Math.Abs(p.WidthMm - 30) < 0.01 && Math.Abs(p.HeightMm - 40) < 0.01);
        Assert.Contains(layout.Placements, p => Math.Abs(p.WidthMm - 40) < 0.01 && Math.Abs(p.HeightMm - 60) < 0.01);
    }

    [Fact]
    public void MixedLabelQuantities_PackTenIndependentPlacements()
    {
        var job = Compile(
            [
                new VariableItemSpec(0, WidthMm: 40, HeightMm: 60, Copies: 4),
                new VariableItemSpec(1, WidthMm: 30, HeightMm: 30, Copies: 6)
            ]);

        var layout = CanvasLayoutEngine.Layout(job);

        Assert.Equal(10, layout.Placements.Count);
        Assert.Equal(1, SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void WidthOnly_DerivesHeightFromTrustedPixelAspect()
    {
        var job = Compile(
            [new VariableItemSpec(0, WidthMm: 50)],
            pixelSizes: [(2000, 1000), (1000, 1000)]);

        var placement = Assert.Single(CanvasLayoutEngine.Layout(job).Placements);

        Assert.Equal(50, placement.WidthMm, 6);
        Assert.Equal(25, placement.HeightMm, 6);
    }

    [Fact]
    public void NoExplicitSize_PreservesTrustedPhysicalPageSize()
    {
        var job = Compile(
            [new VariableItemSpec(0)],
            physicalSizes: [(90d, 54d), (40d, 60d)]);

        var placement = Assert.Single(CanvasLayoutEngine.Layout(job).Placements);

        Assert.Equal(90, placement.WidthMm, 6);
        Assert.Equal(54, placement.HeightMm, 6);
    }

    [Fact]
    public void IndividualRotation_CanMakeLargeItemFit()
    {
        var job = Compile(
            [new VariableItemSpec(
                0,
                WidthMm: 250,
                HeightMm: 150,
                AllowRotate: true)]);

        var placement = Assert.Single(CanvasLayoutEngine.Layout(job).Placements);

        Assert.True(placement.Rotated);
        Assert.Equal(150, placement.WidthMm, 6);
        Assert.Equal(250, placement.HeightMm, 6);
    }

    [Fact]
    public void TooManyItems_AutomaticallyContinueOnNextA4()
    {
        var job = Compile(
            [new VariableItemSpec(
                0,
                WidthMm: 90,
                HeightMm: 90,
                Copies: 8)]);

        Assert.True(SourceJobRenderer.GetOutputPageCount(job) > 1);
    }

    [Fact]
    public void CompleteSets_CompileAsSeparatePackedBatches()
    {
        var plan = Plan(
            [new VariableItemSpec(0, WidthMm: 30, HeightMm: 40)],
            sets: 2);

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(2, compiled.Batches.Count);
        Assert.All(compiled.Batches, batch => Assert.Equal(LayoutMode.Canvas, batch.Job.Layout.Mode));
    }

    private static PrintJobSpec Compile(
        IReadOnlyList<VariableItemSpec> items,
        (int Width, int Height)[]? pixelSizes = null,
        (double Width, double Height)[]? physicalSizes = null)
    {
        var plan = Plan(items, pixelSizes: pixelSizes, physicalSizes: physicalSizes);
        return Assert.Single(PrintPlanCompiler.Compile(plan).Batches).Job;
    }

    private static PrintPlan Plan(
        IReadOnlyList<VariableItemSpec> items,
        int sets = 1,
        (int Width, int Height)[]? pixelSizes = null,
        (double Width, double Height)[]? physicalSizes = null)
    {
        var sources = new List<PlanSourceSpec>();
        for (var index = 0; index < 2; index++)
        {
            IReadOnlyList<SourcePageSizeSpec>? pages =
                physicalSizes is not null && index < physicalSizes.Length
                    ? [new SourcePageSizeSpec(
                        0,
                        physicalSizes[index].Width,
                        physicalSizes[index].Height)]
                    : null;

            sources.Add(new PlanSourceSpec(
                $"C:/print/source-{index}.png",
                1,
                pages,
                pixelSizes is not null && index < pixelSizes.Length
                    ? pixelSizes[index].Width
                    : 1000,
                pixelSizes is not null && index < pixelSizes.Length
                    ? pixelSizes[index].Height
                    : 1000));
        }

        return new PrintPlan(
            "variable items",
            sources,
            [
                new PrintOutputGroupSpec(
                    "Mixed sizes",
                    [
                        new PageSelectionSpec(0, [new PageRangeSpec(1, 1)]),
                        new PageSelectionSpec(1, [new PageRangeSpec(1, 1)])
                    ],
                    new PaperSpec(),
                    new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287),
                    new OutputPrintSettings(),
                    Sets: sets,
                    Collate: true,
                    VariableItems: new VariableItemsSpec(items))
            ],
            new PolicySpec());
    }
}
