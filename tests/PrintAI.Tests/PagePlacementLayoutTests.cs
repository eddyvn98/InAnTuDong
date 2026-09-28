using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class PagePlacementLayoutTests
{
    [Fact]
    public void AsymmetricLeftMargin_ShrinksToFitAndRespectsTwentyMillimetres()
    {
        var job = Job(
            itemWidth: 200,
            itemHeight: 287,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(
                    LeftMm: 20,
                    TopMm: 5,
                    RightMm: 5,
                    BottomMm: 5)));

        var placement = Assert.Single(
            ExactSizeLayoutEngine.Layout(job).Placements);

        Assert.Equal(20, placement.XMm, 6);
        Assert.Equal(185, placement.WidthMm, 6);
        Assert.True(placement.HeightMm < 287);
    }

    [Fact]
    public void UpFiveMillimetres_UsesNegativeYOffset()
    {
        var job = Job(
            itemWidth: 200,
            itemHeight: 287,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                OffsetYMm: -5));

        var placement = Assert.Single(
            ExactSizeLayoutEngine.Layout(job).Placements);

        Assert.Equal(0, placement.YMm, 6);
    }

    [Fact]
    public void RightAnchor_AlignsItemToRightPrintableEdge()
    {
        var job = Job(
            itemWidth: 100,
            itemHeight: 100,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                Anchor: PageAnchor.Right));

        var placement = Assert.Single(
            ExactSizeLayoutEngine.Layout(job).Placements);

        Assert.Equal(105, placement.XMm, 6);
        Assert.Equal(98.5, placement.YMm, 6);
    }

    [Fact]
    public void BottomRightAnchor_AlignsBothEdges()
    {
        var job = Job(
            itemWidth: 100,
            itemHeight: 100,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                Anchor: PageAnchor.BottomRight));

        var placement = Assert.Single(
            ExactSizeLayoutEngine.Layout(job).Placements);

        Assert.Equal(105, placement.XMm, 6);
        Assert.Equal(192, placement.YMm, 6);
    }

    [Fact]
    public void OffsetOutsidePhysicalPaper_IsRejectedByLayout()
    {
        var job = Job(
            itemWidth: 200,
            itemHeight: 287,
            placement: new PagePlacementSpec(
                new PageMarginsSpec(),
                OffsetYMm: -6));

        Assert.Throws<InvalidOperationException>(() =>
            ExactSizeLayoutEngine.Layout(job));
    }

    private static PrintJobSpec Job(
        double itemWidth,
        double itemHeight,
        PagePlacementSpec placement) =>
        new(
            "placement",
            [new SourceSpec("C:/print/doc.pdf")],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                itemWidth,
                itemHeight,
                MarginMm: 5,
                AllowRotate: false,
                PagePlacement: placement),
            new PrintSettings(),
            new PolicySpec());
}
