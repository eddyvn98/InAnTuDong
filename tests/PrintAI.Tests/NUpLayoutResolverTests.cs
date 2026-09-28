using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class NUpLayoutResolverTests
{
    [Theory]
    [InlineData(2, PageOrientation.Landscape, 2, 1)]
    [InlineData(4, PageOrientation.Portrait, 2, 2)]
    [InlineData(6, PageOrientation.Landscape, 3, 2)]
    [InlineData(8, PageOrientation.Landscape, 4, 2)]
    [InlineData(9, PageOrientation.Portrait, 3, 3)]
    [InlineData(16, PageOrientation.Portrait, 4, 4)]
    public void StandardNUp_ResolvesExactCapacityAndOrientation(
        int pagesPerSheet,
        PageOrientation expectedOrientation,
        int expectedColumns,
        int expectedRows)
    {
        var plan = Plan(
            pageCount: pagesPerSheet + 1,
            nUp: new NUpSpec(pagesPerSheet));

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        var layout = GridLayoutEngine.Layout(job);

        Assert.Equal(expectedOrientation, job.Paper.Orientation);
        Assert.Equal(expectedColumns, layout.Columns);
        Assert.Equal(expectedRows, layout.Rows);
        Assert.Equal(pagesPerSheet, layout.CapacityPerPage);
        Assert.Equal(2, SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void EightSlidesLandscape_CanUseTwoColumnsByFourRows()
    {
        var plan = Plan(
            pageCount: 8,
            paper: new PaperSpec(
                210,
                297,
                PageOrientation.Landscape),
            nUp: new NUpSpec(
                PagesPerSheet: 8,
                Columns: 2,
                GapMm: 2,
                MarginMm: 5,
                AutoOrientation: false));

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        var layout = GridLayoutEngine.Layout(job);

        Assert.Equal(PageOrientation.Landscape, job.Paper.Orientation);
        Assert.Equal(2, layout.Columns);
        Assert.Equal(4, layout.Rows);
        Assert.Equal(8, layout.CapacityPerPage);
        Assert.True(job.Layout.ItemWidthMm > job.Layout.ItemHeightMm);
    }

    [Fact]
    public void FourUpDuplex_PreservesSourceOrderAndDuplexIntent()
    {
        var plan = Plan(
            pageCount: 8,
            duplex: DuplexMode.LongEdge,
            nUp: new NUpSpec(4));

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(
            Enumerable.Range(0, 8),
            job.Sources.Select(source => source.PageIndex));
        Assert.Equal(DuplexMode.LongEdge, job.Print.Duplex);
        Assert.Equal(2, SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void NUpGapAndBorder_CompileToExecutableGridLayout()
    {
        var plan = Plan(
            pageCount: 4,
            nUp: new NUpSpec(
                PagesPerSheet: 4,
                GapMm: 4,
                MarginMm: 7,
                Border: true));

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(LayoutMode.Grid, job.Layout.Mode);
        Assert.Equal(4, job.Layout.GapMm);
        Assert.Equal(7, job.Layout.MarginMm);
        Assert.True(job.Layout.ItemBorder);
        Assert.False(job.Layout.AllowRotate);
    }

    [Theory]
    [InlineData(3, null)]
    [InlineData(8, 3)]
    public void InvalidNUp_IsRejected(
        int pagesPerSheet,
        int? columns)
    {
        var plan = Plan(
            pageCount: 8,
            nUp: new NUpSpec(
                PagesPerSheet: pagesPerSheet,
                Columns: columns));

        var validation = PrintPlanValidator.Validate(plan);

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            error => error.Code.StartsWith(
                "plan.groups.nup",
                StringComparison.Ordinal));
    }

    private static PrintPlan Plan(
        int pageCount,
        NUpSpec nUp,
        DuplexMode duplex = DuplexMode.Off,
        PaperSpec? paper = null) =>
        new(
            "n-up",
            [new PlanSourceSpec("C:/print/doc.pdf", pageCount)],
            [
                new PrintOutputGroupSpec(
                    Name: "N-up pages",
                    Selections:
                    [
                        new PageSelectionSpec(
                            0,
                            [new PageRangeSpec(1, pageCount)])
                    ],
                    Paper: paper ?? new PaperSpec(),
                    Layout: new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287,
                        MarginMm: 5,
                        AllowRotate: false),
                    Print: new OutputPrintSettings(
                        Duplex: duplex),
                    NUp: nUp)
            ],
            new PolicySpec());
}
