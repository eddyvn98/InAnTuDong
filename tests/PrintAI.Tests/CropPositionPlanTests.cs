using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class CropPositionPlanTests
{
    [Fact]
    public void PlacementAndCrop_CompileIntoExecutableLayout()
    {
        var plan = Plan(
            placement: new PagePlacementSpec(
                new PageMarginsSpec(
                    LeftMm: 20,
                    TopMm: 5,
                    RightMm: 5,
                    BottomMm: 5),
                Anchor: PageAnchor.Right),
            crop: new SourceCropSpec(
                SourceCropMode.AutoTrimWhite));

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.NotNull(job.Layout.PagePlacement);
        Assert.Equal(
            PageAnchor.Right,
            job.Layout.PagePlacement!.Anchor);
        Assert.NotNull(job.Layout.SourceCrop);
        Assert.Equal(
            SourceCropMode.AutoTrimWhite,
            job.Layout.SourceCrop!.Mode);

        var placement = Assert.Single(
            LayoutEngine.Layout(job).Placements);

        Assert.True(placement.XMm >= 20);
    }

    [Fact]
    public void EdgesMmCrop_RequiresTrustedPhysicalPageSize()
    {
        var plan = Plan(
            crop: new SourceCropSpec(
                SourceCropMode.EdgesMm,
                new CropEdgesSpec(TopMm: 10)),
            pages: null);

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error =>
                error.Code ==
                "plan.groups.crop.sourceSize");
    }

    [Fact]
    public void EdgesMmCrop_AcceptsTrustedPhysicalPageSize()
    {
        var plan = Plan(
            crop: new SourceCropSpec(
                SourceCropMode.EdgesMm,
                new CropEdgesSpec(TopMm: 10)),
            pages:
            [
                new SourcePageSizeSpec(
                    0,
                    210,
                    297)
            ]);

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(
            210,
            job.Sources[0].OriginalWidthMm);
        Assert.Equal(
            SourceCropMode.EdgesMm,
            job.Layout.SourceCrop!.Mode);
    }

    [Fact]
    public void CropAndPhysicalScaling_AreRejected()
    {
        var plan = Plan(
            crop: new SourceCropSpec(
                SourceCropMode.AutoTrimWhite),
            scaling: new PhysicalScaleSpec(
                PhysicalScaleMode.MaxFit));

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error =>
                error.Code ==
                "plan.groups.crop.scaling");
    }

    [Fact]
    public void PlacementAndNUp_AreRejected()
    {
        var group = Group(
            placement: new PagePlacementSpec(
                new PageMarginsSpec()),
            crop: null,
            scaling: null) with
        {
            NUp = new NUpSpec(4)
        };

        var plan = new PrintPlan(
            "invalid",
            [new PlanSourceSpec("C:/print/doc.pdf", 4)],
            [group with
            {
                Selections =
                [
                    new PageSelectionSpec(
                        0,
                        [new PageRangeSpec(1, 4)])
                ]
            }],
            new PolicySpec());

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error =>
                error.Code ==
                "plan.groups.placement.nup");
    }

    private static PrintPlan Plan(
        PagePlacementSpec? placement = null,
        SourceCropSpec? crop = null,
        PhysicalScaleSpec? scaling = null,
        IReadOnlyList<SourcePageSizeSpec>? pages = null) =>
        new(
            "crop-position",
            [new PlanSourceSpec(
                "C:/print/doc.pdf",
                1,
                pages)],
            [Group(placement, crop, scaling)],
            new PolicySpec());

    private static PrintOutputGroupSpec Group(
        PagePlacementSpec? placement,
        SourceCropSpec? crop,
        PhysicalScaleSpec? scaling) =>
        new(
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
                200,
                287,
                MarginMm: 5,
                AllowRotate: false),
            Print: new OutputPrintSettings(),
            Scaling: scaling,
            Placement: placement,
            Crop: crop);
}
