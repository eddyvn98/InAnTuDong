using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class SimpleA4MonochromePlannerTests
{
    [Fact]
    public void TryPlan_AsksOnlyMissingDuplexChoice()
    {
        var request = Request("In trắng đen A4");

        Assert.True(SimpleA4MonochromePlanner.TryPlan(request, out var outcome));
        Assert.Equal(new[] { "Bạn muốn in 1 mặt hay 2 mặt (duplex)?" }, outcome.Questions);
        Assert.Equal(3, outcome.Job.Sources.Count);
        Assert.Equal(200d, outcome.Job.Layout.ItemWidthMm);
        Assert.Equal(287d, outcome.Job.Layout.ItemHeightMm);
    }

    [Fact]
    public void TryPlan_UsesClarificationAnswerWithoutCallingModel()
    {
        var request = Request(
            "In trắng đen A4\n\n" +
            "Câu hỏi làm rõ của AGY: Bạn muốn in 1 mặt hay 2 mặt (duplex)?\n" +
            "Trả lời của người dùng: in một mặt");

        Assert.True(SimpleA4MonochromePlanner.TryPlan(request, out var outcome));
        Assert.Empty(outcome.Questions);
        Assert.Equal(1, outcome.Confidence);
        Assert.Equal(PrintAI.Domain.ColorMode.Grayscale, outcome.Job.Print.ColorMode);
        Assert.Equal(PrintAI.Domain.DuplexMode.Off, outcome.Job.Print.Duplex);
        Assert.Equal(new[] { 0, 1, 2 }, outcome.Job.Sources.Select(source => source.PageIndex));
    }

    [Theory]
    [InlineData("In A4 trắng đen, 2 bản, một mặt")]
    [InlineData("In A4 trắng đen, 2 trang/tờ, một mặt")]
    [InlineData("In màu A4, một mặt")]
    public void TryPlan_LeavesUnsupportedRequestsToAntigravity(string userRequest)
    {
        Assert.False(SimpleA4MonochromePlanner.TryPlan(
            Request(userRequest), out _));
    }

    private static PlanningRequest Request(string userRequest) => new(
        userRequest,
        [new PlanningSource(
            "/tmp/document.pdf",
            "Pdf",
            PageCount: 3)]);
}
