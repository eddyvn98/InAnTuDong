using System.Text.Json.Nodes;
using PrintAI.Domain;
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
    public void Parse_AcceptsNUpIntent()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        var firstGroup = node["plan"]!["outputGroups"]![0]!.AsObject();

        firstGroup["nUp"] = new JsonObject
        {
            ["pagesPerSheet"] = 4,
            ["columns"] = 2,
            ["gapMm"] = 2,
            ["marginMm"] = 5,
            ["border"] = true,
            ["fit"] = "contain",
            ["autoOrientation"] = false
        };

        var outcome = GeneralPrintPlanParser.Parse(node.ToJsonString());
        var nUp = outcome.Plan.OutputGroups[0].NUp;

        Assert.NotNull(nUp);
        Assert.Equal(4, nUp.PagesPerSheet);
        Assert.Equal(2, nUp.Columns);
        Assert.True(nUp.Border);
    }

    [Fact]
    public void Parse_AcceptsPhysicalScalingIntent()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        var firstGroup = node["plan"]!["outputGroups"]![0]!.AsObject();

        firstGroup["scaling"] = new JsonObject
        {
            ["mode"] = "percent",
            ["percent"] = 80
        };

        var outcome = GeneralPrintPlanParser.Parse(
            node.ToJsonString());

        var scaling = outcome.Plan.OutputGroups[0].Scaling;

        Assert.NotNull(scaling);
        Assert.Equal(
            PhysicalScaleMode.Percent,
            scaling!.Mode);
        Assert.Equal(80, scaling.Percent);
    }

    [Fact]
    public void Parse_AcceptsPlacementAndCropIntent()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        var firstGroup =
            node["plan"]!["outputGroups"]![0]!.AsObject();

        firstGroup["placement"] = new JsonObject
        {
            ["margins"] = new JsonObject
            {
                ["leftMm"] = 20,
                ["topMm"] = 5,
                ["rightMm"] = 5,
                ["bottomMm"] = 5
            },
            ["anchor"] = "right",
            ["offsetXMm"] = 0,
            ["offsetYMm"] = -5,
            ["shrinkToFit"] = true
        };

        firstGroup["crop"] = new JsonObject
        {
            ["mode"] = "edgesMm",
            ["edgesMm"] = new JsonObject
            {
                ["leftMm"] = 0,
                ["topMm"] = 10,
                ["rightMm"] = 0,
                ["bottomMm"] = 0
            },
            ["whiteThreshold"] = 245
        };

        var outcome = GeneralPrintPlanParser.Parse(
            node.ToJsonString());

        var group = outcome.Plan.OutputGroups[0];

        Assert.NotNull(group.Placement);
        Assert.Equal(
            PageAnchor.Right,
            group.Placement!.Anchor);
        Assert.Equal(-5, group.Placement.OffsetYMm);

        Assert.NotNull(group.Crop);
        Assert.Equal(
            SourceCropMode.EdgesMm,
            group.Crop!.Mode);
        Assert.Equal(10, group.Crop.EdgesMm!.TopMm);
    }

    [Fact]
    public void Parse_AcceptsBookletIntent()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        var firstGroup =
            node["plan"]!["outputGroups"]![0]!.AsObject();

        firstGroup["booklet"] = new JsonObject
        {
            ["gutterMm"] = 10,
            ["marginMm"] = 5
        };

        var outcome = GeneralPrintPlanParser.Parse(
            node.ToJsonString());

        var booklet = outcome.Plan.OutputGroups[0].Booklet;

        Assert.NotNull(booklet);
        Assert.Equal(10, booklet!.GutterMm);
        Assert.Equal(5, booklet.MarginMm);
    }

    [Fact]
    public void Parse_AcceptsPosterIntent()
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        var firstGroup =
            node["plan"]!["outputGroups"]![0]!.AsObject();

        firstGroup["poster"] = new JsonObject
        {
            ["targetWidthMm"] = 600,
            ["targetHeightMm"] = 900,
            ["columns"] = null,
            ["rows"] = null,
            ["overlapMm"] = 10,
            ["marginMm"] = 5,
            ["registrationMarks"] = true,
            ["tileLabels"] = true,
            ["autoOrientation"] = true,
            ["fit"] = "contain"
        };

        firstGroup["print"]!["duplex"] = "off";

        var outcome = GeneralPrintPlanParser.Parse(
            node.ToJsonString());

        var poster = outcome.Plan.OutputGroups[0].Poster;

        Assert.NotNull(poster);
        Assert.Equal(600, poster!.TargetWidthMm);
        Assert.Equal(900, poster.TargetHeightMm);
        Assert.Equal(10, poster.OverlapMm);
        Assert.True(poster.RegistrationMarks);
        Assert.True(poster.TileLabels);
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
