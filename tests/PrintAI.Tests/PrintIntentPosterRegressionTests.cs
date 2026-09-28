using PrintAI.Domain;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentPosterRegressionTests
{
    [Fact]
    public void Poster001_SixtyByNinetyCentimetres_UsesMultipleA4Sheets()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 600,
                TargetHeightMm: 900));

        Assert.Equal(15, SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void Poster002_A2Target_UsesEightA4Tiles()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 420,
                TargetHeightMm: 594));

        Assert.Equal(8, job.Sources.Count);
    }

    [Fact]
    public void Poster003_FiftyBySeventyCentimetres_IsTiled()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 500,
                TargetHeightMm: 700));

        Assert.Equal(LayoutMode.PosterTile, job.Layout.Mode);
        Assert.Equal(8, job.Sources.Count);
    }

    [Fact]
    public void Poster004_ExplicitThreeByThreeGrid_UsesNineSheets()
    {
        var job = Compile(
            new PosterSpec(
                Columns: 3,
                Rows: 3),
            pixelWidth: 3000,
            pixelHeight: 2000);

        Assert.Equal(9, job.Sources.Count);
        Assert.All(
            job.Sources,
            source =>
            {
                Assert.Equal(3, source.PosterTile!.Rows);
                Assert.Equal(3, source.PosterTile.Columns);
            });
    }

    [Fact]
    public void Poster005_FiveMillimetreOverlap_IsPreserved()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 600,
                TargetHeightMm: 900,
                OverlapMm: 5));

        Assert.All(
            job.Sources,
            source => Assert.Equal(
                5,
                source.PosterTile!.OverlapMm));
    }

    [Fact]
    public void Poster006_OneMetreWide_DerivesHeightFromSourceAspect()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 1000),
            pixelWidth: 4000,
            pixelHeight: 2000);

        Assert.All(
            job.Sources,
            source => Assert.Equal(
                500,
                source.PosterTile!.TargetHeightMm,
                6));
    }

    [Fact]
    public void Poster007_RegistrationMarks_AreEnabledOnTiles()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 600,
                TargetHeightMm: 900,
                RegistrationMarks: true));

        Assert.All(
            job.Sources,
            source => Assert.True(
                source.PosterTile!.RegistrationMarks));
    }

    [Fact]
    public void Poster008_SeventyByOneHundredCentimetres_UsesTenMillimetreOverlap()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 700,
                TargetHeightMm: 1000,
                OverlapMm: 10));

        Assert.Equal(PageOrientation.Portrait, job.Paper.Orientation);
        Assert.Equal(16, job.Sources.Count);
        Assert.All(
            job.Sources,
            source => Assert.Equal(
                10,
                source.PosterTile!.OverlapMm));
    }

    [Fact]
    public void Poster009_MinimumSheets_UsesTrustedPhysicalMapSizeAndBestOrientation()
    {
        var job = Compile(
            new PosterSpec(),
            pixelWidth: null,
            pixelHeight: null,
            physicalWidthMm: 420,
            physicalHeightMm: 594);

        Assert.Equal(PageOrientation.Landscape, job.Paper.Orientation);
        Assert.Equal(8, job.Sources.Count);
    }

    [Fact]
    public void Poster010_TileNumbers_AreEnabled()
    {
        var job = Compile(
            new PosterSpec(
                TargetWidthMm: 600,
                TargetHeightMm: 900,
                TileLabels: true));

        Assert.All(
            job.Sources,
            source => Assert.True(
                source.PosterTile!.TileLabel));
    }

    private static PrintJobSpec Compile(
        PosterSpec poster,
        int? pixelWidth = 4000,
        int? pixelHeight = 3000,
        double? physicalWidthMm = null,
        double? physicalHeightMm = null)
    {
        IReadOnlyList<SourcePageSizeSpec>? pages =
            physicalWidthMm is double width &&
            physicalHeightMm is double height
                ? [new SourcePageSizeSpec(0, width, height)]
                : null;

        var plan = new PrintPlan(
            "poster regression",
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
                    "Poster",
                    [new PageSelectionSpec(
                        0,
                        [new PageRangeSpec(1, 1)])],
                    new PaperSpec(),
                    new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287),
                    new OutputPrintSettings(),
                    Poster: poster)
            ],
            new PolicySpec());

        return Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;
    }
}
