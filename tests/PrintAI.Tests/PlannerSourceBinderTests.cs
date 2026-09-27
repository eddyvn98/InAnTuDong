using PrintAI.Domain;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class PlannerSourceBinderTests
{
    [Fact]
    public void BindToAllowedSources_AcceptsExactApprovedPath()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "printai-approved.png");

        var job = CreateJob(path);

        var bound = PlannerSourceBinder.BindToAllowedSources(
            job,
            [path]);

        Assert.Equal(Path.GetFullPath(path), bound.Sources[0].Path);
    }

    [Fact]
    public void BindToAllowedSources_RejectsInventedPath()
    {
        var approved = Path.Combine(
            Path.GetTempPath(),
            "printai-approved.png");

        var invented = Path.Combine(
            Path.GetTempPath(),
            "printai-invented.png");

        var error = Assert.Throws<PlanningFormatException>(() =>
            PlannerSourceBinder.BindToAllowedSources(
                CreateJob(invented),
                [approved]));

        Assert.Contains(
            "unapproved source path",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PrintJobSpec CreateJob(string path) =>
        new(
            "source binding",
            [new SourceSpec(path)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid,
                40,
                60,
                MarginMm: 5),
            new PrintSettings(),
            new PolicySpec());
}
