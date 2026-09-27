using PrintAI.Domain;
using PrintAI.Layout;

namespace PrintAI.Tests;

public sealed class GridLayoutEngineTests
{
    [Fact]
    public void A4_40x60_WithRotation_Fits18PerPage()
    {
        var job = CreateJob(sourceCopies: 20, allowRotate: true);

        var result = GridLayoutEngine.Layout(job);

        Assert.True(result.Rotated);
        Assert.Equal(3, result.Columns);
        Assert.Equal(6, result.Rows);
        Assert.Equal(18, result.CapacityPerPage);
        Assert.Equal(20, result.Placements.Count);
        Assert.Equal(2, result.Placements.Max(p => p.Page) + 1);
    }

    [Fact]
    public void A4_40x60_WithoutRotation_Fits16PerPage()
    {
        var job = CreateJob(sourceCopies: 20, allowRotate: false);

        var result = GridLayoutEngine.Layout(job);

        Assert.False(result.Rotated);
        Assert.Equal(4, result.Columns);
        Assert.Equal(4, result.Rows);
        Assert.Equal(16, result.CapacityPerPage);
    }

    [Fact]
    public void RejectsPaperLargerThanA4()
    {
        var job = CreateJob(sourceCopies: 1, allowRotate: false) with
        {
            Paper = new PaperSpec(216, 356)
        };

        var result = PrintJobValidator.Validate(job);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "paper.max");
    }

    private static PrintJobSpec CreateJob(int sourceCopies, bool allowRotate) => new(
        "test",
        [new SourceSpec("sample.jpg", sourceCopies)],
        new PaperSpec(),
        new LayoutSpec(
            LayoutMode.Grid,
            ItemWidthMm: 40,
            ItemHeightMm: 60,
            GapMm: 3,
            MarginMm: 5,
            AllowRotate: allowRotate),
        new PrintSettings(),
        new PolicySpec());
}
