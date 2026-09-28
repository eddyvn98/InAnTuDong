using PrintAI.Domain;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class BookletPrintPlanTests
{
    [Fact]
    public void BookletCompiler_ForcesLandscapeShortEdgeAndImposition()
    {
        var plan = Plan(
            pageCount: 8,
            sets: 1);

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(PageOrientation.Landscape, job.Paper.Orientation);
        Assert.Equal(DuplexMode.ShortEdge, job.Print.Duplex);
        Assert.Equal(LayoutMode.Grid, job.Layout.Mode);
        Assert.Equal(4, SourceJobRenderer.GetOutputPageCount(job));
        Assert.Equal(
            [7, 0, 1, 6, 5, 2, 3, 4],
            job.Sources.Select(source => source.PageIndex).ToArray());
    }

    [Fact]
    public void TwoBookletSets_CompileAsTwoCompleteBatches()
    {
        var compiled = PrintPlanCompiler.Compile(
            Plan(
                pageCount: 8,
                sets: 2));

        Assert.Equal(2, compiled.Batches.Count);
        Assert.Equal([1, 2],
            compiled.Batches.Select(batch => batch.SetNumber).ToArray());

        foreach (var batch in compiled.Batches)
        {
            Assert.Equal(8, batch.Job.Sources.Count);
            Assert.Equal(DuplexMode.ShortEdge, batch.Job.Print.Duplex);
        }
    }

    [Fact]
    public void BookletRejectsNonCollatedSets()
    {
        var plan = Plan(
            pageCount: 8,
            sets: 2,
            collate: false);

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error => error.Code ==
                "plan.groups.booklet.collate");
    }

    [Fact]
    public void BookletRejectsNUpCombination()
    {
        var plan = Plan(
            pageCount: 8,
            sets: 1,
            nUp: new NUpSpec(4));

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error => error.Code ==
                "plan.groups.booklet.nup");
    }

    [Fact]
    public void TwentyPages_NeedNoPaddingButStillProduceFiveSheets()
    {
        var job = Assert.Single(
            PrintPlanCompiler.Compile(
                Plan(
                    pageCount: 20,
                    sets: 1))
            .Batches).Job;

        Assert.DoesNotContain(
            job.Sources,
            source => source.IsBlank);
        Assert.Equal(10, SourceJobRenderer.GetOutputPageCount(job));
    }

    private static PrintPlan Plan(
        int pageCount,
        int sets,
        bool collate = true,
        NUpSpec? nUp = null) =>
        new(
            "booklet plan",
            [new PlanSourceSpec(
                "C:/print/doc.pdf",
                pageCount)],
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
                    Print: new OutputPrintSettings(
                        Duplex: DuplexMode.Off),
                    Sets: sets,
                    Collate: collate,
                    NUp: nUp,
                    Booklet: new BookletSpec())
            ],
            new PolicySpec());
}
