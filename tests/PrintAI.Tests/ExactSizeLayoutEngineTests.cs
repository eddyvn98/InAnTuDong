using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class ExactSizeLayoutEngineTests
{
    [Fact]
    public void ExactSize_CentersOneItemPerPage_WithoutScaling()
    {
        var job = CreateJob(40, 60, copies: 2, allowRotate: false);

        var result = LayoutEngine.Layout(job);

        Assert.Equal(1, result.CapacityPerPage);
        Assert.Equal(2, result.Placements.Count);

        var first = result.Placements[0];
        Assert.Equal(40, first.WidthMm, 6);
        Assert.Equal(60, first.HeightMm, 6);
        Assert.Equal(85, first.XMm, 6);
        Assert.Equal(118.5, first.YMm, 6);
        Assert.Equal(0, first.Page);
        Assert.Equal(1, result.Placements[1].Page);
    }

    [Fact]
    public void ExactSize_RotatesOnlyWhenNeededToFit()
    {
        var job = CreateJob(250, 100, copies: 1, allowRotate: true);

        var result = LayoutEngine.Layout(job);

        Assert.True(result.Rotated);
        Assert.Equal(100, result.Placements[0].WidthMm, 6);
        Assert.Equal(250, result.Placements[0].HeightMm, 6);
    }

    [Fact]
    public void ExactSize_RejectsItemThatCannotFit()
    {
        var job = CreateJob(290, 250, copies: 1, allowRotate: true);

        Assert.Throws<InvalidOperationException>(() => LayoutEngine.Layout(job));
    }

    private static PrintJobSpec CreateJob(
        double width,
        double height,
        int copies,
        bool allowRotate) => new(
            "exact",
            [new SourceSpec("sample.jpg", copies)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: width,
                ItemHeightMm: height,
                MarginMm: 5,
                AllowRotate: allowRotate),
            new PrintSettings(),
            new PolicySpec());
}
