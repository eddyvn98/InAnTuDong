using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Workflows;
using Xunit;

namespace PrintAI.Tests;

public sealed class MixedCompositionWorkflowTests
{
    [Fact]
    public void CreateJob_PreservesSourceOrderPagesAndCopies()
    {
        var job = MixedCompositionWorkflow.CreateJob(
            [
                new SourceSpec("a.pdf", Copies: 2, PageIndex: 1),
                new SourceSpec("b.png", Copies: 3, PageIndex: 0)
            ],
            new MixedCompositionOptions(
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                Fit: FitMode.Cover));

        Assert.Equal(2, job.Sources.Count);
        Assert.Equal("a.pdf", job.Sources[0].Path);
        Assert.Equal(1, job.Sources[0].PageIndex);
        Assert.Equal(2, job.Sources[0].Copies);
        Assert.Equal("b.png", job.Sources[1].Path);
        Assert.Equal(3, job.Sources[1].Copies);
        Assert.Equal(FitMode.Cover, job.Layout.Fit);
        Assert.Equal(PreviewPolicy.Required, job.Policy.Preview);
    }

    [Fact]
    public void Layout_ExpandsMixedContentInSourceOrderAcrossPages()
    {
        var job = MixedCompositionWorkflow.CreateJob(
            [
                new SourceSpec("a.png", Copies: 3),
                new SourceSpec("b.png", Copies: 2)
            ],
            new MixedCompositionOptions(
                ItemWidthMm: 100,
                ItemHeightMm: 100,
                GapMm: 0,
                MarginMm: 5,
                AllowRotate: false));

        var layout = LayoutEngine.Layout(job);

        Assert.Equal(5, layout.Placements.Count);
        Assert.Equal(new[] { 0, 0, 0, 1, 1 }, layout.Placements.Select(p => p.SourceIndex).ToArray());
        Assert.Equal(0, layout.Placements[3].Page);
        Assert.Equal(1, layout.Placements[4].Page);
    }

    [Fact]
    public void CreateJob_RejectsMoreThanOneThousandItems()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MixedCompositionWorkflow.CreateJob(
                [
                    new SourceSpec("a.png", Copies: 600),
                    new SourceSpec("b.png", Copies: 401)
                ],
                new MixedCompositionOptions(40, 60)));
    }

    [Fact]
    public void CreateJob_RejectsItemThatCannotFitA4()
    {
        Assert.Throws<ArgumentException>(() =>
            MixedCompositionWorkflow.CreateJob(
                [new SourceSpec("a.png")],
                new MixedCompositionOptions(
                    ItemWidthMm: 220,
                    ItemHeightMm: 100,
                    AllowRotate: false)));
    }
}
