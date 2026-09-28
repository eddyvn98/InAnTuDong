using PrintAI.Domain;
using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class CanvasMultiPageTests
{
    [Fact]
    public void CanvasPlacementPage_IsPreserved()
    {
        var job = new PrintJobSpec(
            "canvas pages",
            [new SourceSpec("C:/print/a.png")],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Canvas,
                1,
                1,
                Canvas: new CanvasLayoutSpec(
                [
                    new CanvasPlacementSpec(0, 5, 5, 30, 40, Page: 0),
                    new CanvasPlacementSpec(0, 5, 5, 30, 40, Page: 1)
                ])),
            new PrintSettings(),
            new PolicySpec());

        var layout = CanvasLayoutEngine.Layout(job);

        Assert.Equal([0, 1], layout.Placements.Select(p => p.Page).ToArray());
    }

    [Fact]
    public void NinetyDegreeCanvasRotation_UsesRotatedFootprintFlag()
    {
        var job = new PrintJobSpec(
            "rotated canvas",
            [new SourceSpec("C:/print/a.png")],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Canvas,
                1,
                1,
                Canvas: new CanvasLayoutSpec(
                [
                    new CanvasPlacementSpec(
                        0, 5, 5, 60, 40,
                        UseRotatedFootprint: true)
                ])),
            new PrintSettings(),
            new PolicySpec());

        var placement = Assert.Single(CanvasLayoutEngine.Layout(job).Placements);
        Assert.True(placement.Rotated);
    }
}
