using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentVariableItemsRegressionTests
{
    [Fact]
    public void Var001_ThreeByFourAndFourBySixShareOneA4()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 30, HeightMm: 40),
            new VariableItemSpec(1, WidthMm: 40, HeightMm: 60)
        ]);

        Assert.Single(Pages(job));
    }

    [Fact]
    public void Var002_LogoQrAndPhotoKeepIndependentSizes()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 20, HeightMm: 20),
            new VariableItemSpec(1, WidthMm: 30, HeightMm: 30),
            new VariableItemSpec(2, WidthMm: 50, HeightMm: 70)
        ]);

        var sizes = CanvasLayoutEngine.Layout(job).Placements
            .Select(p => (p.WidthMm, p.HeightMm))
            .ToHashSet();

        Assert.Contains((20d, 20d), sizes);
        Assert.Contains((30d, 30d), sizes);
        Assert.Contains((50d, 70d), sizes);
    }

    [Fact]
    public void Var003_ThreeImagesCanAllUseDifferentPhysicalSizes()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 30, HeightMm: 40),
            new VariableItemSpec(1, WidthMm: 45, HeightMm: 60),
            new VariableItemSpec(2, WidthMm: 50, HeightMm: 70)
        ]);

        Assert.Equal(
            3,
            CanvasLayoutEngine.Layout(job)
                .Placements
                .Select(p => (p.WidthMm, p.HeightMm))
                .Distinct()
                .Count());
    }

    [Fact]
    public void Var004_MixedLabelQuantitiesUseTenPlacements()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 40, HeightMm: 60, Copies: 4),
            new VariableItemSpec(1, WidthMm: 30, HeightMm: 30, Copies: 6)
        ]);

        Assert.Equal(10, CanvasLayoutEngine.Layout(job).Placements.Count);
    }

    [Fact]
    public void Var005_WidthOnlyItemsDeriveHeightFromTrustedAspect()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 50),
            new VariableItemSpec(1, WidthMm: 80)
        ],
        pixelSizes:
        [
            (2000, 1000),
            (1000, 2000),
            (1000, 1000)
        ]);

        var placements = CanvasLayoutEngine.Layout(job).Placements;

        Assert.Contains(placements, p =>
            Math.Abs(p.WidthMm - 50) < 0.01 &&
            Math.Abs(p.HeightMm - 25) < 0.01);
        Assert.Contains(placements, p =>
            Math.Abs(p.WidthMm - 80) < 0.01 &&
            Math.Abs(p.HeightMm - 160) < 0.01);
    }

    [Fact]
    public void Var006_CardAndLabelCanShareOneSheet()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 90, HeightMm: 54),
            new VariableItemSpec(1, WidthMm: 40, HeightMm: 60)
        ]);

        Assert.Single(Pages(job));
    }

    [Fact]
    public void Var007_EachSourceCanPreserveTrustedPhysicalSize()
    {
        var job = Compile(
        [
            new VariableItemSpec(0),
            new VariableItemSpec(1),
            new VariableItemSpec(2)
        ],
        physicalSizes:
        [
            (90d, 54d),
            (40d, 60d),
            (30d, 30d)
        ]);

        var sizes = CanvasLayoutEngine.Layout(job).Placements
            .Select(p => (p.WidthMm, p.HeightMm))
            .ToHashSet();

        Assert.Contains((90d, 54d), sizes);
        Assert.Contains((40d, 60d), sizes);
        Assert.Contains((30d, 30d), sizes);
    }

    [Fact]
    public void Var008_TwoSmallQrAndOneLargeLogoKeepQuantities()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 25, HeightMm: 25, Copies: 2),
            new VariableItemSpec(1, WidthMm: 60, HeightMm: 60)
        ]);

        Assert.Equal(3, CanvasLayoutEngine.Layout(job).Placements.Count);
    }

    [Fact]
    public void Var009_IndividualRotationCanBeUsedByPacker()
    {
        var job = Compile(
        [
            new VariableItemSpec(
                0,
                WidthMm: 250,
                HeightMm: 150,
                AllowRotate: true)
        ]);

        Assert.True(
            Assert.Single(CanvasLayoutEngine.Layout(job).Placements)
                .Rotated);
    }

    [Fact]
    public void Var010_ThreeLabelTypesCanShareOnePackedSheet()
    {
        var job = Compile(
        [
            new VariableItemSpec(0, WidthMm: 40, HeightMm: 60, Copies: 2),
            new VariableItemSpec(1, WidthMm: 30, HeightMm: 30, Copies: 3),
            new VariableItemSpec(2, WidthMm: 50, HeightMm: 20, Copies: 4)
        ]);

        Assert.Single(Pages(job));
        Assert.Equal(9, CanvasLayoutEngine.Layout(job).Placements.Count);
    }

    private static IReadOnlyList<int> Pages(PrintJobSpec job) =>
        CanvasLayoutEngine.Layout(job).Placements
            .Select(p => p.Page)
            .Distinct()
            .ToArray();

    private static PrintJobSpec Compile(
        IReadOnlyList<VariableItemSpec> items,
        (int Width, int Height)[]? pixelSizes = null,
        (double Width, double Height)[]? physicalSizes = null)
    {
        var sources = new List<PlanSourceSpec>();

        for (var index = 0; index < 3; index++)
        {
            IReadOnlyList<SourcePageSizeSpec>? pages =
                physicalSizes is not null &&
                index < physicalSizes.Length
                    ? [new SourcePageSizeSpec(
                        0,
                        physicalSizes[index].Width,
                        physicalSizes[index].Height)]
                    : null;

            sources.Add(new PlanSourceSpec(
                $"C:/print/source-{index}.png",
                1,
                pages,
                pixelSizes is not null &&
                index < pixelSizes.Length
                    ? pixelSizes[index].Width
                    : 1000,
                pixelSizes is not null &&
                index < pixelSizes.Length
                    ? pixelSizes[index].Height
                    : 1000));
        }

        var plan = new PrintPlan(
            "variable regression",
            sources,
            [
                new PrintOutputGroupSpec(
                    "Mixed sizes",
                    [
                        new PageSelectionSpec(0, [new PageRangeSpec(1, 1)]),
                        new PageSelectionSpec(1, [new PageRangeSpec(1, 1)]),
                        new PageSelectionSpec(2, [new PageRangeSpec(1, 1)])
                    ],
                    new PaperSpec(),
                    new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287),
                    new OutputPrintSettings(),
                    VariableItems: new VariableItemsSpec(items))
            ],
            new PolicySpec());

        return Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;
    }
}
