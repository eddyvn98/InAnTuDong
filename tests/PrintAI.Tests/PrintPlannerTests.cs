using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintPlannerTests
{
    [Fact]
    public async Task PlanAsync_UsesProviderNeutralClientAndParsesResult()
    {
        var client = new FakePlannerClient(
            """
            {
              "job": {
                "jobName": "A4 document",
                "sources": [{ "path": "C:/print/a.png", "copies": 1 }],
                "paper": { "widthMm": 210, "heightMm": 297, "orientation": "portrait" },
                "layout": {
                  "mode": "grid",
                  "itemWidthMm": 200,
                  "itemHeightMm": 287,
                  "gapMm": 0,
                  "marginMm": 5,
                  "allowRotate": false,
                  "cutMarks": false,
                  "fit": "contain"
                },
                "print": {
                  "copies": 1,
                  "colorMode": "color",
                  "quality": "standard",
                  "duplex": "off"
                },
                "policy": { "preview": "required" },
                "schemaVersion": "1.0"
              },
              "confidence": 0.93,
              "questions": [],
              "warnings": []
            }
            """);

        var planner = new PrintPlanner(client);
        var result = await planner.PlanAsync(
            new PlanningRequest(
                "In file này A4 một bản.",
                [new PlanningSource("C:/print/a.png", "Png", 1200, 1600)]));

        Assert.Equal("A4 document", result.Job.JobName);
        Assert.Equal(1, client.CallCount);
        Assert.Contains("millimetres", client.LastRequest!.SystemInstruction);
        Assert.Contains("In file này A4 một bản.", client.LastRequest.UserPayload);
    }

    private sealed class FakePlannerClient(string response) : IPlannerModelClient
    {
        public int CallCount { get; private set; }
        public PlannerModelRequest? LastRequest { get; private set; }

        public Task<string> CompleteAsync(
            PlannerModelRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
