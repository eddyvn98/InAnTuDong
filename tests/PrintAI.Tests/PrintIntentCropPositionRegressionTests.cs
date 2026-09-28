using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentCropPositionRegressionTests
{
    [Fact]
    public void Pos003_AutoTrimWhite_IsExplicitCropIntent()
    {
        var job = Job(
            crop: new SourceCropSpec(
                SourceCropMode.AutoTrimWhite));

        Assert.Equal(
            SourceCropMode.AutoTrimWhite,
            job.Layout.SourceCrop!.Mode);
    }

    [Fact]
    public void Pos004_LeftTwentyMillimetres_ProducesMinimumLeftMargin()
    {
        var job = Job(
            placement: new PagePlacementSpec(
                new PageMarginsSpec(
                    LeftMm: 20,
                    TopMm: 5,
                    RightMm: 5,
                    BottomMm: 5)));

        var placement = Assert.Single(
            LayoutEngine.Layout(job).Placements);

        Assert.True(placement.XMm >= 20);
    }

    [Fact]
    public void Pos005_MoveUpFiveMillimetres_ReachesTopPaperEdge()
    {
        var job = Job(
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                OffsetYMm: -5));

        var placement = Assert.Single(
            LayoutEngine.Layout(job).Placements);

        Assert.Equal(0, placement.YMm, 6);
    }

    [Fact]
    public void Pos006_RightAnchor_AlignsToRightPrintableEdge()
    {
        var job = Job(
            itemWidth: 100,
            itemHeight: 100,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                Anchor: PageAnchor.Right));

        var placement = Assert.Single(
            LayoutEngine.Layout(job).Placements);

        Assert.Equal(105, placement.XMm, 6);
    }

    [Fact]
    public void Pos007_CenterCrop_UsesTargetAspect()
    {
        using var bitmap = new SKBitmap(200, 100);

        var crop = SourceCropCalculator.Calculate(
            bitmap,
            new SourceSpec("image.png"),
            targetWidthPx: 100,
            targetHeightPx: 100,
            crop: new SourceCropSpec(
                SourceCropMode.CenterToTargetAspect));

        Assert.Equal(50, crop.Left, 5);
        Assert.Equal(150, crop.Right, 5);
    }

    [Fact]
    public void Pos008_TopTenMillimetres_MapsToPhysicalEdgeCrop()
    {
        var job = Job(
            crop: new SourceCropSpec(
                SourceCropMode.EdgesMm,
                new CropEdgesSpec(TopMm: 10)),
            originalWidthMm: 210,
            originalHeightMm: 297);

        Assert.Equal(
            10,
            job.Layout.SourceCrop!.EdgesMm!.TopMm);
        Assert.Equal(
            210,
            job.Sources[0].OriginalWidthMm);
    }

    [Fact]
    public void Pos009_TopFifteenLeftTwentyFive_ArePreserved()
    {
        var job = Job(
            placement: new PagePlacementSpec(
                new PageMarginsSpec(
                    LeftMm: 25,
                    TopMm: 15,
                    RightMm: 5,
                    BottomMm: 5)));

        var margins = job.Layout.PagePlacement!.Margins;

        Assert.Equal(25, margins.LeftMm);
        Assert.Equal(15, margins.TopMm);
    }

    [Fact]
    public void Pos010_BottomRightAnchor_AlignsBothDirections()
    {
        var job = Job(
            itemWidth: 100,
            itemHeight: 100,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                Anchor: PageAnchor.BottomRight));

        var placement = Assert.Single(
            LayoutEngine.Layout(job).Placements);

        Assert.Equal(105, placement.XMm, 6);
        Assert.Equal(192, placement.YMm, 6);
    }

    private static PrintJobSpec Job(
        double itemWidth = 200,
        double itemHeight = 287,
        PagePlacementSpec? placement = null,
        SourceCropSpec? crop = null,
        double? originalWidthMm = null,
        double? originalHeightMm = null)
    {
        var planSource = new PlanSourceSpec(
            "C:/print/doc.pdf",
            1,
            originalWidthMm is double width &&
            originalHeightMm is double height
                ? [new SourcePageSizeSpec(0, width, height)]
                : null);

        var plan = new PrintPlan(
            "position regression",
            [planSource],
            [
                new PrintOutputGroupSpec(
                    Name: "page",
                    Selections:
                    [
                        new PageSelectionSpec(
                            0,
                            [new PageRangeSpec(1, 1)])
                    ],
                    Paper: new PaperSpec(),
                    Layout: new LayoutSpec(
                        LayoutMode.ExactSize,
                        itemWidth,
                        itemHeight,
                        MarginMm: 5,
                        AllowRotate: false),
                    Print: new OutputPrintSettings(),
                    Placement: placement,
                    Crop: crop)
            ],
            new PolicySpec());

        return Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;
    }
}
