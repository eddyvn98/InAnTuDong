using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using PrintAI.Workflows;
using Xunit;

namespace PrintAI.Tests;

public sealed class LabelSheetWorkflowTests
{
    [Fact]
    public void CustomLabelSheet_PreservesPhysicalSettingsAndSourcePage()
    {
        var job = LabelSheetWorkflow.CreateJob(
            new SourceSpec("labels.pdf", PageIndex: 4),
            new LabelSheetOptions(
                ItemWidthMm: 35,
                ItemHeightMm: 25,
                Copies: 30,
                GapMm: 1.5,
                MarginMm: 6,
                AllowRotate: true,
                CutMarks: true,
                Fit: FitMode.Contain));

        Assert.Equal(35, job.Layout.ItemWidthMm);
        Assert.Equal(25, job.Layout.ItemHeightMm);
        Assert.Equal(1.5, job.Layout.GapMm);
        Assert.Equal(6, job.Layout.MarginMm);
        Assert.Equal(30, job.Sources[0].Copies);
        Assert.Equal(4, job.Sources[0].PageIndex);
        Assert.True(job.Layout.CutMarks);
        Assert.Equal(FitMode.Contain, job.Layout.Fit);
    }

    [Fact]
    public void CustomLabelSheet_PaginatesInsteadOfShrinkingItems()
    {
        var job = LabelSheetWorkflow.CreateJob(
            new SourceSpec("label.png"),
            new LabelSheetOptions(
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                Copies: 24,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: true));

        var layout = LayoutEngine.Layout(job);

        Assert.Equal(18, layout.CapacityPerPage);
        Assert.Equal(24, layout.Placements.Count);
        Assert.Equal(2, SourceJobRenderer.GetOutputPageCount(job));
        Assert.All(
            layout.Placements,
            p => Assert.True(
                Math.Abs(p.WidthMm - 60) < 0.001 &&
                Math.Abs(p.HeightMm - 40) < 0.001));
    }

    [Fact]
    public void CustomLabelSheet_RejectsItemThatCannotFitA4()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            LabelSheetWorkflow.CreateJob(
                new SourceSpec("label.png"),
                new LabelSheetOptions(
                    ItemWidthMm: 220,
                    ItemHeightMm: 310,
                    Copies: 1,
                    MarginMm: 5,
                    AllowRotate: true)));

        Assert.Contains("không thể đặt vừa", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void CustomLabelSheet_RejectsUnsafeCopyCounts(int copies)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LabelSheetWorkflow.CreateJob(
                new SourceSpec("label.png"),
                new LabelSheetOptions(
                    ItemWidthMm: 40,
                    ItemHeightMm: 60,
                    Copies: copies)));
    }
}
