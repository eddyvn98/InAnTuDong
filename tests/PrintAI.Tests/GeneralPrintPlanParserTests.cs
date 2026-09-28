using System.Text.Json.Nodes;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class GeneralPrintPlanParserTests
{
    [Fact]
    public void Parse_AcceptsStrictPrintPlanEnvelope()
    {
        var outcome = GeneralPrintPlanParser.Parse(ValidJson());

        Assert.Equal("2.0", outcome.Plan.SchemaVersion);
        Assert.Equal("Mixed print", outcome.Plan.PlanName);
        Assert.Equal(2, outcome.Plan.OutputGroups.Count);
        Assert.Equal(0.97, outcome.Confidence, 2);
    }

    [Fact]
    public void Parse_RejectsUnknownFields()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        node["customer"] = "not allowed";

        var error = Assert.Throws<PlanningFormatException>(() =>
            GeneralPrintPlanParser.Parse(node.ToJsonString()));

        Assert.Contains("schema", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsUnsupportedPlanSchema()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        node["plan"]!["schemaVersion"] = "3.0";

        var error = Assert.Throws<PlanningFormatException>(() =>
            GeneralPrintPlanParser.Parse(node.ToJsonString()));

        Assert.Contains("plan.schema.version", error.Message);
    }

    private static string ValidJson() =>
        """
        {
          "plan": {
            "planName": "Mixed print",
            "sources": [
              { "path": "C:/print/doc.pdf", "pageCount": 20 }
            ],
            "outputGroups": [
              {
                "name": "Cover",
                "selections": [
                  {
                    "sourceIndex": 0,
                    "include": [{ "startPage": 1, "endPage": 1 }],
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
                  "fit": "contain"
                },
                "print": {
                  "colorMode": "color",
                  "quality": "standard",
                  "duplex": "off"
                },
                "sets": 3,
                "collate": true,
                "sequence": 0
              },
              {
                "name": "Body",
                "selections": [
                  {
                    "sourceIndex": 0,
                    "include": [{ "startPage": 2, "endPage": 20 }],
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
                  "fit": "contain"
                },
                "print": {
                  "colorMode": "grayscale",
                  "quality": "standard",
                  "duplex": "longEdge"
                },
                "sets": 3,
                "collate": true,
                "sequence": 1
              }
            ],
            "policy": { "preview": "required" },
            "schemaVersion": "2.0"
          },
          "confidence": 0.97,
          "questions": [],
          "warnings": []
        }
        """;
}
