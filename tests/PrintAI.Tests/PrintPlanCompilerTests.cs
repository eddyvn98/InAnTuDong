using PrintAI.Domain;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintPlanCompilerTests
{
    [Fact]
    public void Compile_SplitsMixedColorAndDuplexIntoOrderedExecutionJobs()
    {
        var plan = new PrintPlan(
            "Mixed document",
            [new("C:/print/doc.pdf", PageCount: 20)],
            [
                Group(
                    "Cover",
                    sequence: 0,
                    selections: [new(0, [new(1, 1)])],
                    color: ColorMode.Color,
                    duplex: DuplexMode.Off),
                Group(
                    "Body",
                    sequence: 1,
                    selections: [new(0, [new(2, 20)])],
                    color: ColorMode.Grayscale,
                    duplex: DuplexMode.LongEdge)
            ],
            new());

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(2, compiled.Batches.Count);

        var cover = compiled.Batches[0].Job;
        Assert.Equal(ColorMode.Color, cover.Print.ColorMode);
        Assert.Equal(DuplexMode.Off, cover.Print.Duplex);
        Assert.Single(cover.Sources);
        Assert.Equal(0, cover.Sources[0].PageIndex);

        var body = compiled.Batches[1].Job;
        Assert.Equal(ColorMode.Grayscale, body.Print.ColorMode);
        Assert.Equal(DuplexMode.LongEdge, body.Print.Duplex);
        Assert.Equal(19, body.Sources.Count);
        Assert.Equal(1, body.Sources[0].PageIndex);
        Assert.Equal(19, body.Sources[^1].PageIndex);
    }

    [Fact]
    public void Compile_MultiGroupCollatedSetsInterleaveGroupsPerCompleteSet()
    {
        var plan = new PrintPlan(
            "Three complete documents",
            [new("C:/print/doc.pdf", PageCount: 3)],
            [
                Group(
                    "Cover",
                    sequence: 0,
                    selections: [new(0, [new(1, 1)])],
                    color: ColorMode.Color,
                    sets: 3,
                    collate: true),
                Group(
                    "Body",
                    sequence: 1,
                    selections: [new(0, [new(2, 3)])],
                    color: ColorMode.Grayscale,
                    duplex: DuplexMode.LongEdge,
                    sets: 3,
                    collate: true)
            ],
            new());

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(6, compiled.Batches.Count);
        Assert.Equal(
            ["Cover - set 1/3", "Body - set 1/3",
             "Cover - set 2/3", "Body - set 2/3",
             "Cover - set 3/3", "Body - set 3/3"],
            compiled.Batches.Select(batch => batch.Job.JobName).ToArray());
        Assert.Equal([1, 1, 2, 2, 3, 3], compiled.Batches.Select(batch => batch.SetNumber).ToArray());
    }

    [Fact]
    public void Compile_CollatedSetsBecomeCompleteRepeatedBatches()
    {
        var plan = new PrintPlan(
            "Five sets",
            [
                new("C:/print/a.pdf", PageCount: 2),
                new("C:/print/b.pdf", PageCount: 1)
            ],
            [
                Group(
                    "Set",
                    sequence: 0,
                    selections:
                    [
                        new(0, [new(1, 2)]),
                        new(1, [new(1, 1)])
                    ],
                    sets: 5,
                    collate: true)
            ],
            new());

        var compiled = PrintPlanCompiler.Compile(plan);

        Assert.Equal(5, compiled.Batches.Count);
        Assert.Equal([1, 2, 3, 4, 5], compiled.Batches.Select(batch => batch.SetNumber));

        foreach (var batch in compiled.Batches)
        {
            Assert.Equal(3, batch.Job.Sources.Count);
            Assert.Equal("C:/print/a.pdf", batch.Job.Sources[0].Path);
            Assert.Equal(0, batch.Job.Sources[0].PageIndex);
            Assert.Equal("C:/print/a.pdf", batch.Job.Sources[1].Path);
            Assert.Equal(1, batch.Job.Sources[1].PageIndex);
            Assert.Equal("C:/print/b.pdf", batch.Job.Sources[2].Path);
            Assert.Equal(0, batch.Job.Sources[2].PageIndex);
            Assert.All(batch.Job.Sources, source => Assert.Equal(1, source.Copies));
        }
    }

    [Fact]
    public void Compile_NonCollatedSetsUsePerPageCopiesInOneBatch()
    {
        var plan = new PrintPlan(
            "Uncollated copies",
            [new("C:/print/doc.pdf", PageCount: 2)],
            [
                Group(
                    "Copies",
                    sequence: 0,
                    selections: [new(0, [new(1, 2)])],
                    sets: 3,
                    collate: false)
            ],
            new());

        var compiled = PrintPlanCompiler.Compile(plan);

        var batch = Assert.Single(compiled.Batches);
        Assert.Equal(0, batch.SetNumber);
        Assert.Equal(2, batch.Job.Sources.Count);
        Assert.All(batch.Job.Sources, source => Assert.Equal(3, source.Copies));
    }

    private static PrintOutputGroupSpec Group(
        string name,
        int sequence,
        IReadOnlyList<PageSelectionSpec> selections,
        ColorMode color = ColorMode.Color,
        DuplexMode duplex = DuplexMode.Off,
        int sets = 1,
        bool collate = true) =>
        new(
            Name: name,
            Selections: selections,
            Paper: new(),
            Layout: new(
                LayoutMode.ExactSize,
                ItemWidthMm: 200,
                ItemHeightMm: 287,
                MarginMm: 5,
                AllowRotate: false),
            Print: new(color, PrintQuality.Standard, duplex),
            Sets: sets,
            Collate: collate,
            Sequence: sequence);
}
