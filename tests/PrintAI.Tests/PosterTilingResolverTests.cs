using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class PosterTilingResolverTests
{
    [Fact]
    public void SixtyByNinetyCentimetres_ChoosesFifteenLandscapeSheets()
    {
        var result = Resolve(
            new PosterSpec(
                TargetWidthMm: 600,
                TargetHeightMm: 900));

        Assert.Equal(PageOrientation.Landscape, result.Paper.Orientation);
        Assert.Equal(3, result.Columns);
        Assert.Equal(5, result.Rows);
        Assert.Equal(15, result.Sources.Count);
        Assert.Equal(600, result.TargetWidthMm);
        Assert.Equal(900, result.TargetHeightMm);

        var lastColumn = result.Sources[2].PosterTile!;
        Assert.Equal(36, lastColumn.CanvasWidthMm, 6);

        var lastRow = result.Sources[^1].PosterTile!;
        Assert.Equal(120, lastRow.CanvasHeightMm, 6);
    }

    [Fact]
    public void A2Target_UsesMinimumEightA4Sheets()
    {
        var result = Resolve(
            new PosterSpec(
                TargetWidthMm: 420,
                TargetHeightMm: 594));

        Assert.Equal(PageOrientation.Landscape, result.Paper.Orientation);
        Assert.Equal(2, result.Columns);
        Assert.Equal(4, result.Rows);
        Assert.Equal(8, result.Sources.Count);
    }

    [Fact]
    public void ExplicitThreeByThreeGrid_ProducesExactlyNineTiles()
    {
        var result = Resolve(
            new PosterSpec(
                Columns: 3,
                Rows: 3),
            pixelWidth: 3000,
            pixelHeight: 2000);

        Assert.Equal(3, result.Columns);
        Assert.Equal(3, result.Rows);
        Assert.Equal(9, result.Sources.Count);
        Assert.True(result.TargetWidthMm > 0);
        Assert.True(result.TargetHeightMm > 0);
    }

    [Fact]
    public void OneMetreWide_DerivesHeightFromTrustedPixelAspect()
    {
        var result = Resolve(
            new PosterSpec(
                TargetWidthMm: 1000),
            pixelWidth: 4000,
            pixelHeight: 2000);

        Assert.Equal(1000, result.TargetWidthMm);
        Assert.Equal(500, result.TargetHeightMm, 6);
    }

    [Fact]
    public void OverlapTenMillimetres_IsPreservedAndAffectsStride()
    {
        var result = Resolve(
            new PosterSpec(
                TargetWidthMm: 700,
                TargetHeightMm: 1000,
                OverlapMm: 10));

        Assert.Equal(PageOrientation.Portrait, result.Paper.Orientation);
        Assert.Equal(4, result.Columns);
        Assert.Equal(4, result.Rows);

        var first = result.Sources[0].PosterTile!;
        var second = result.Sources[1].PosterTile!;

        Assert.Equal(10, first.OverlapMm);
        Assert.Equal(
            first.CanvasWidthMm - 10,
            second.CanvasXmm,
            6);
    }

    [Fact]
    public void TrustedPhysicalPageSize_CanBePosterTargetFallback()
    {
        var plan = Plan(
            new PosterSpec(),
            pixelWidth: null,
            pixelHeight: null,
            physicalWidthMm: 420,
            physicalHeightMm: 594);

        var group = plan.OutputGroups[0];
        var source = new SourceSpec(
            "C:/print/map.pdf",
            OriginalWidthMm: 420,
            OriginalHeightMm: 594);

        var result = PosterTilingResolver.Resolve(
            plan,
            group,
            [source]);

        Assert.Equal(420, result.TargetWidthMm);
        Assert.Equal(594, result.TargetHeightMm);
        Assert.Equal(8, result.Sources.Count);
    }

    [Fact]
    public void RegistrationMarksAndTileLabels_AreCopiedToEveryTile()
    {
        var result = Resolve(
            new PosterSpec(
                TargetWidthMm: 400,
                TargetHeightMm: 400,
                RegistrationMarks: true,
                TileLabels: true));

        Assert.All(
            result.Sources,
            source =>
            {
                Assert.True(source.PosterTile!.RegistrationMarks);
                Assert.True(source.PosterTile.TileLabel);
            });
    }

    [Fact]
    public void LastPartialTile_ProducesOneOutputPageAtItsOwnPhysicalSize()
    {
        var result = Resolve(
            new PosterSpec(
                TargetWidthMm: 350,
                TargetHeightMm: 200,
                AutoOrientation: false,
                OverlapMm: 0));

        var job = new PrintJobSpec(
            "poster",
            result.Sources,
            result.Paper,
            result.Layout,
            new PrintSettings(),
            new PolicySpec());

        var layout = PosterTileLayoutEngine.Layout(job);

        Assert.Equal(result.Sources.Count, layout.Placements.Count);
        Assert.Equal(result.Sources.Count, SourceJobRenderer.GetOutputPageCount(job));

        var last = layout.Placements[^1];
        Assert.Equal(150, last.WidthMm, 6);
        Assert.Equal(200, last.HeightMm, 6);
    }

    private static PosterTilingResult Resolve(
        PosterSpec poster,
        int? pixelWidth = 4000,
        int? pixelHeight = 3000)
    {
        var plan = Plan(
            poster,
            pixelWidth,
            pixelHeight);

        return PosterTilingResolver.Resolve(
            plan,
            plan.OutputGroups[0],
            [new SourceSpec("C:/print/poster.png")]);
    }

    private static PrintPlan Plan(
        PosterSpec poster,
        int? pixelWidth,
        int? pixelHeight,
        double? physicalWidthMm = null,
        double? physicalHeightMm = null)
    {
        IReadOnlyList<SourcePageSizeSpec>? pages =
            physicalWidthMm is double width &&
            physicalHeightMm is double height
                ? [new SourcePageSizeSpec(0, width, height)]
                : null;

        return new(
            "poster",
            [
                new PlanSourceSpec(
                    "C:/print/poster.png",
                    1,
                    pages,
                    pixelWidth,
                    pixelHeight)
            ],
            [
                new PrintOutputGroupSpec(
                    Name: "Poster",
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
                        287),
                    Print: new OutputPrintSettings(),
                    Poster: poster)
            ],
            new PolicySpec());
    }
}
