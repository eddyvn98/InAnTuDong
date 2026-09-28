using System.Text.Json;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class GeneralPrintPlannerTests
{
    [Fact]
    public async Task PlanAsync_UsesExplicitPrintPlanSchemaAndPrintOnlyScope()
    {
        var client = new FakePlannerClient(
            """
            {
              "plan": {
                "planName": "Document",
                "sources": [
                  { "path": "C:/print/doc.pdf", "pageCount": 2 }
                ],
                "outputGroups": [
                  {
                    "name": "All pages",
                    "selections": [
                      {
                        "sourceIndex": 0,
                        "include": [{ "startPage": 1, "endPage": 2 }],
                        "exclude": [],
                        "parity": "all"
                      }
                    ],
                    "paper": {
                      "widthMm": 210,
                      "heightMm": 297,
                      "orientation": "portrait"
                    },
                    "layout": {
                      "mode": "exactSize",
                      "itemWidthMm": 200,
                      "itemHeightMm": 287,
                      "gapMm": 0,
                      "marginMm": 5,
                      "allowRotate": false,
                      "cutMarks": false,
                      "fit": "contain",
                      "canvas": null
                    },
                    "print": {
                      "colorMode": "color",
                      "quality": "standard",
                      "duplex": "off"
                    },
                    "sets": 1,
                    "collate": true,
                    "sequence": 0
                  }
                ],
                "policy": { "preview": "required" },
                "schemaVersion": "2.0"
              },
              "confidence": 0.96,
              "questions": [],
              "warnings": []
            }
            """);

        var planner = new GeneralPrintPlanner(client);
        var outcome = await planner.PlanAsync(
            new PlanningRequest(
                "In file này A4.",
                [new PlanningSource(
                    "C:/print/doc.pdf",
                    "Pdf",
                    PageCount: 2)]));

        Assert.Equal("2.0", outcome.Plan.SchemaVersion);
        Assert.Contains("Required JSON shape", client.LastRequest!.SystemInstruction);
        Assert.Contains("Do not use pricing", client.LastRequest.SystemInstruction);
        Assert.Contains("complete multi-group document", client.LastRequest.SystemInstruction);
        Assert.Contains("pagesPerSheet", client.LastRequest.SystemInstruction);
        Assert.Contains("Supported pagesPerSheet values are exactly: 2, 4, 6, 8, 9, 16", client.LastRequest.SystemInstruction);
        Assert.Contains("shrinkOnly", client.LastRequest.SystemInstruction);
        Assert.Contains("\"mode\": \"percent\"", client.LastRequest.SystemInstruction);
        Assert.Contains("\"mode\": \"maxFit\"", client.LastRequest.SystemInstruction);

        using var payload = JsonDocument.Parse(client.LastRequest.UserPayload);
        Assert.Equal(
            "In file này A4.",
            payload.RootElement.GetProperty("request").GetString());
    }

    private sealed class FakePlannerClient(string response) : IPlannerModelClient
    {
        public PlannerModelRequest? LastRequest { get; private set; }

        public Task<string> CompleteAsync(
            PlannerModelRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
