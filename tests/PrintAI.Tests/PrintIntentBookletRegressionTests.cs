using PrintAI.Domain;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentBookletRegressionTests
{
    [Fact]
    public void Book001_BasicBooklet_UsesImposition()
    {
        var job = Compile(pageCount: 8);

        Assert.Equal(
            [7, 0, 1, 6, 5, 2, 3, 4],
            job.Sources.Select(source => source.PageIndex).ToArray());
    }

    [Fact]
    public void Book002_A4FoldToA5_UsesLandscapeTwoUp()
    {
        var job = Compile(pageCount: 8);

        Assert.Equal(PageOrientation.Landscape, job.Paper.Orientation);
        Assert.Equal(LayoutMode.Grid, job.Layout.Mode);
        Assert.Equal(2, PrintAI.Layout.GridLayoutEngine.Layout(job).Columns);
    }

    [Fact]
    public void Book003_TwoSidedBooklet_UsesShortEdge()
    {
        var job = Compile(pageCount: 8);

        Assert.Equal(DuplexMode.ShortEdge, job.Print.Duplex);
    }

    [Fact]
    public void Book004_SixteenPages_ProduceFourSheets()
    {
        var job = Compile(pageCount: 16);

        Assert.Equal(8, SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void Book005_WiderCenterMargin_UsesRequestedGutter()
    {
        var job = Compile(
            pageCount: 8,
            booklet: new BookletSpec(
                GutterMm: 10,
                MarginMm: 5));

        Assert.Equal(10, job.Layout.GapMm);
    }

    [Fact]
    public void Book006_SmallBook_ReordersAutomatically()
    {
        var job = Compile(pageCount: 4);

        Assert.Equal(
            [3, 0, 1, 2],
            job.Sources.Select(source => source.PageIndex).ToArray());
    }

    [Fact]
    public void Book007_TwoSets_RemainTwoCompleteBooklets()
    {
        var compiled = CompilePlan(
            pageCount: 8,
            sets: 2);

        Assert.Equal(2, compiled.Batches.Count);
        Assert.All(
            compiled.Batches,
            batch => Assert.Equal(
                8,
                batch.Job.Sources.Count));
    }

    [Fact]
    public void Book008_BlankPadding_IsAddedWhenNeeded()
    {
        var job = Compile(pageCount: 18);

        Assert.Equal(20, job.Sources.Count);
        Assert.Equal(2, job.Sources.Count(source => source.IsBlank));
    }

    [Fact]
    public void Book009_TwentyPages_AreAlreadyCompleteSignature()
    {
        var job = Compile(pageCount: 20);

        Assert.Equal(20, job.Sources.Count);
        Assert.DoesNotContain(
            job.Sources,
            source => source.IsBlank);
    }

    [Fact]
    public void Book010_LandscapeFold_PreservesLandscapePaper()
    {
        var job = Compile(pageCount: 8);

        Assert.Equal(PageOrientation.Landscape, job.Paper.Orientation);
        Assert.True(job.Layout.ItemHeightMm < job.Paper.HeightMm);
    }

    private static PrintJobSpec Compile(
        int pageCount,
        BookletSpec? booklet = null) =>
        Assert.Single(
            CompilePlan(
                pageCount,
                booklet: booklet)
            .Batches).Job;

    private static CompiledPrintPlan CompilePlan(
        int pageCount,
        int sets = 1,
        BookletSpec? booklet = null)
    {
        var plan = new PrintPlan(
            "booklet regression",
            [
                new PlanSourceSpec(
                    "C:/print/doc.pdf",
                    pageCount)
            ],
            [
                new PrintOutputGroupSpec(
                    Name: "Booklet",
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
                        287),
                    Print: new OutputPrintSettings(),
                    Sets: sets,
                    Collate: true,
                    Booklet: booklet ?? new BookletSpec())
            ],
            new PolicySpec());

        return PrintPlanCompiler.Compile(plan);
    }
}
