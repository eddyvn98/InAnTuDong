using PrintAI.Domain;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentRequestRegressionTests
{
    [Fact]
    public void Page003_OddPagesCompileInDocumentOrder()
    {
        var plan = Plan(
            [new PlanSourceSpec("C:/print/doc.pdf", 20)],
            [
                Group(
                    "Odd pages",
                    [new PageSelectionSpec(
                        0,
                        [new PageRangeSpec(1, 19)],
                        Parity: PageParity.Odd)])
            ]);

        var batch = Assert.Single(PrintPlanCompiler.Compile(plan).Batches);

        Assert.Equal(
            [0, 2, 4, 6, 8, 10, 12, 14, 16, 18],
            batch.Job.Sources.Select(source => source.PageIndex).ToArray());
    }

    [Fact]
    public void Color010_ThreeCompleteSetsInterleaveColorAndGrayscaleGroups()
    {
        var plan = Plan(
            [new PlanSourceSpec("C:/print/doc.pdf", 20)],
            [
                Group(
                    "Cover",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 1)])],
                    color: ColorMode.Color,
                    sets: 3,
                    sequence: 0),
                Group(
                    "Body",
                    [new PageSelectionSpec(0, [new PageRangeSpec(2, 20)])],
                    color: ColorMode.Grayscale,
                    sets: 3,
                    sequence: 1)
            ]);

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(6, compiled.Batches.Count);
        Assert.Equal(
            [ColorMode.Color, ColorMode.Grayscale,
             ColorMode.Color, ColorMode.Grayscale,
             ColorMode.Color, ColorMode.Grayscale],
            compiled.Batches.Select(batch => batch.Job.Print.ColorMode).ToArray());
        Assert.Equal(
            [1, 1, 2, 2, 3, 3],
            compiled.Batches.Select(batch => batch.SetNumber).ToArray());
    }

    [Fact]
    public void Multi003_MultiFileSelectionPreservesExplicitSourceAndPageOrder()
    {
        var plan = Plan(
            [
                new PlanSourceSpec("C:/print/a.pdf", 10),
                new PlanSourceSpec("C:/print/b.pdf", 10)
            ],
            [
                Group(
                    "A then B",
                    [
                        new PageSelectionSpec(0, [new PageRangeSpec(1, 3)]),
                        new PageSelectionSpec(1, [new PageRangeSpec(2, 2)])
                    ])
            ]);

        var batch = Assert.Single(PrintPlanCompiler.Compile(plan).Batches);

        Assert.Equal(
            [
                ("C:/print/a.pdf", 0),
                ("C:/print/a.pdf", 1),
                ("C:/print/a.pdf", 2),
                ("C:/print/b.pdf", 1)
            ],
            batch.Job.Sources
                .Select(source => (source.Path, source.PageIndex))
                .ToArray());
    }

    [Fact]
    public void Set006_NonCollatedRequestCompilesPerPageCopies()
    {
        var plan = Plan(
            [new PlanSourceSpec("C:/print/doc.pdf", 3)],
            [
                Group(
                    "Three copies each",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 3)])],
                    sets: 3,
                    collate: false)
            ]);

        var batch = Assert.Single(PrintPlanCompiler.Compile(plan).Batches);

        Assert.Equal(3, batch.Job.Sources.Count);
        Assert.All(batch.Job.Sources, source => Assert.Equal(3, source.Copies));
    }

    [Fact]
    public void Dup004_CoverSimplexAndBodyDuplexRemainSeparateBatches()
    {
        var plan = Plan(
            [new PlanSourceSpec("C:/print/doc.pdf", 20)],
            [
                Group(
                    "Cover",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 1)])],
                    duplex: DuplexMode.Off,
                    sequence: 0),
                Group(
                    "Body",
                    [new PageSelectionSpec(0, [new PageRangeSpec(2, 20)])],
                    duplex: DuplexMode.LongEdge,
                    sequence: 1)
            ]);

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(2, compiled.Batches.Count);
        Assert.Equal(DuplexMode.Off, compiled.Batches[0].Job.Print.Duplex);
        Assert.Equal(DuplexMode.LongEdge, compiled.Batches[1].Job.Print.Duplex);
        Assert.Single(compiled.Batches[0].Job.Sources);
        Assert.Equal(19, compiled.Batches[1].Job.Sources.Count);
    }

    [Fact]
    public void Paper004_MixedPaperAndOrientationRemainGroupSpecific()
    {
        var plan = Plan(
            [new PlanSourceSpec("C:/print/doc.pdf", 2)],
            [
                Group(
                    "A4 portrait",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 1)])],
                    paper: new PaperSpec(210, 297, PageOrientation.Portrait),
                    sequence: 0),
                Group(
                    "A5 landscape",
                    [new PageSelectionSpec(0, [new PageRangeSpec(2, 2)])],
                    paper: new PaperSpec(148, 210, PageOrientation.Landscape),
                    sequence: 1)
            ]);

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(2, compiled.Batches.Count);
        Assert.Equal(210, compiled.Batches[0].Job.Paper.WidthMm);
        Assert.Equal(PageOrientation.Portrait, compiled.Batches[0].Job.Paper.Orientation);
        Assert.Equal(148, compiled.Batches[1].Job.Paper.WidthMm);
        Assert.Equal(PageOrientation.Landscape, compiled.Batches[1].Job.Paper.Orientation);
    }

    private static PrintPlan Plan(
        IReadOnlyList<PlanSourceSpec> sources,
        IReadOnlyList<PrintOutputGroupSpec> groups) =>
        new(
            "request-regression",
            sources,
            groups,
            new PolicySpec(PreviewPolicy.Required));

    private static PrintOutputGroupSpec Group(
        string name,
        IReadOnlyList<PageSelectionSpec> selections,
        ColorMode color = ColorMode.Color,
        DuplexMode duplex = DuplexMode.Off,
        int sets = 1,
        bool collate = true,
        int sequence = 0,
        PaperSpec? paper = null) =>
        new(
            name,
            selections,
            paper ?? new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                200,
                287,
                MarginMm: 5,
                AllowRotate: false),
            new OutputPrintSettings(color, PrintQuality.Standard, duplex),
            sets,
            collate,
            sequence);
}
