using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Workflows;
using Xunit;

namespace PrintAI.Tests;

public sealed class AutoLayoutWorkflowTests
{
    [Fact]
    public void ThreeItems_GeneratesSeveralDistinct4x6Candidates()
    {
        var candidates = AutoLayoutWorkflow.Generate4x6(
            [
                new SourceSpec("a.png"),
                new SourceSpec("b.png"),
                new SourceSpec("c.png")
            ]);

        Assert.True(candidates.Count >= 3);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(AutoLayoutWorkflow.FourBySixWidthMm, candidate.Job.Paper.WidthMm, 3);
            Assert.Equal(AutoLayoutWorkflow.FourBySixHeightMm, candidate.Job.Paper.HeightMm, 3);
            Assert.Equal(PreviewPolicy.Required, candidate.Job.Policy.Preview);
        });

        Assert.Equal(
            candidates.Count,
            candidates.Select(candidate => candidate.Id).Distinct().Count());
    }

    [Fact]
    public void Candidates_KeepEveryPlacementInsideRequestedPaper()
    {
        var candidates = AutoLayoutWorkflow.Generate4x6(
            [
                new SourceSpec("a.png"),
                new SourceSpec("b.png"),
                new SourceSpec("c.png")
            ],
            AutoLayoutPreference.Fill);

        foreach (var candidate in candidates)
        {
            var layout = LayoutEngine.Layout(candidate.Job);
            Assert.Equal(3, layout.Placements.Count);

            var paperWidth = candidate.Job.Paper.Orientation == PageOrientation.Portrait
                ? candidate.Job.Paper.WidthMm
                : candidate.Job.Paper.HeightMm;
            var paperHeight = candidate.Job.Paper.Orientation == PageOrientation.Portrait
                ? candidate.Job.Paper.HeightMm
                : candidate.Job.Paper.WidthMm;

            Assert.All(layout.Placements, placement =>
            {
                Assert.True(placement.XMm >= 0);
                Assert.True(placement.YMm >= 0);
                Assert.True(placement.XMm + placement.WidthMm <= paperWidth + 0.01);
                Assert.True(placement.YMm + placement.HeightMm <= paperHeight + 0.01);
            });
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Generate4x6_RejectsUnsupportedItemCount(int count)
    {
        var sources = Enumerable.Range(0, count)
            .Select(index => new SourceSpec($"{index}.png"))
            .ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AutoLayoutWorkflow.Generate4x6(sources));
    }

    [Fact]
    public void FillPreference_UsesCover()
    {
        var candidate = AutoLayoutWorkflow.Generate4x6(
            [new SourceSpec("a.png"), new SourceSpec("b.png")],
            AutoLayoutPreference.Fill)
            .First();

        Assert.Equal(FitMode.Cover, candidate.Job.Layout.Fit);
    }
}
