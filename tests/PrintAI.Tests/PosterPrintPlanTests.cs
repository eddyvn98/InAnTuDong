using PrintAI.Domain;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class PosterPrintPlanTests
{
    [Fact]
    public void PosterCompiler_ProducesSimplexTileJob()
    {
        var plan = Plan(
            new PosterSpec(
                TargetWidthMm: 500,
                TargetHeightMm: 700));

        var job = Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;

        Assert.Equal(LayoutMode.PosterTile, job.Layout.Mode);
        Assert.Equal(DuplexMode.Off, job.Print.Duplex);
        Assert.All(
            job.Sources,
            source =>
            {
                Assert.NotNull(source.PosterTile);
                Assert.Equal(1, source.Copies);
            });
        Assert.Equal(
            job.Sources.Count,
            SourceJobRenderer.GetOutputPageCount(job));
    }

    [Fact]
    public void TwoPosterSets_AreTwoCompleteTileBatches()
    {
        var plan = Plan(
            new PosterSpec(
                TargetWidthMm: 400,
                TargetHeightMm: 400),
            sets: 2);

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(2, compiled.Batches.Count);
        Assert.Equal(
            compiled.Batches[0].Job.Sources.Count,
            compiled.Batches[1].Job.Sources.Count);
    }

    [Fact]
    public void PosterRejectsNUpCombination()
    {
        var plan = Plan(
            new PosterSpec(
                TargetWidthMm: 400,
                TargetHeightMm: 400),
            nUp: new NUpSpec(4));

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error => error.Code ==
                "plan.groups.poster.combination");
    }

    [Fact]
    public void PosterRejectsMultipleSelectedPages()
    {
        var plan = new PrintPlan(
            "poster",
            [new PlanSourceSpec(
                "C:/print/poster.pdf",
                2,
                PixelWidth: 2000,
                PixelHeight: 1000)],
            [
                new PrintOutputGroupSpec(
                    "Poster",
                    [new PageSelectionSpec(
                        0,
                        [new PageRangeSpec(1, 2)])],
                    new PaperSpec(),
                    new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287),
                    new OutputPrintSettings(),
                    Poster: new PosterSpec(
                        TargetWidthMm: 600,
                        TargetHeightMm: 300))
            ],
            new PolicySpec());

        var validation = PrintPlanValidator.Validate(plan);

        Assert.Contains(
            validation.Errors,
            error => error.Code ==
                "plan.groups.poster.pages");
    }

    [Fact]
    public void WidthOnlyPoster_RequiresTrustedAspectAtCompileTime()
    {
        var plan = new PrintPlan(
            "poster",
            [new PlanSourceSpec(
                "C:/print/poster.png",
                1)],
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
                    Poster: new PosterSpec(
                        TargetWidthMm: 1000))
            ],
            new PolicySpec());

        var error = Assert.Throws<ArgumentException>(() =>
            PrintPlanCompiler.Compile(plan));

        Assert.Contains(
            "aspect ratio",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PrintPlan Plan(
        PosterSpec poster,
        int sets = 1,
        NUpSpec? nUp = null) =>
        new(
            "poster",
            [
                new PlanSourceSpec(
                    "C:/print/poster.png",
                    1,
                    PixelWidth: 4000,
                    PixelHeight: 3000)
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
                    Sets: sets,
                    Collate: true,
                    NUp: nUp,
                    Poster: poster)
            ],
            new PolicySpec());
}
