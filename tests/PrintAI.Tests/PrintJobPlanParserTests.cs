using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintJobPlanParserTests
{
    [Fact]
    public void Parse_AcceptsStrictValidEnvelope()
    {
        var outcome = PrintJobPlanParser.Parse(ValidJson());

        Assert.Equal("1.0", outcome.Job.SchemaVersion);
        Assert.Equal("4x6 photos", outcome.Job.JobName);
        Assert.Equal(0.96, outcome.Confidence, 2);
        Assert.Empty(outcome.Questions);
    }

    [Fact]
    public void Parse_RejectsUnknownFields()
    {
        var json = ValidJson().Replace(
            ""warnings":[]",
            ""warnings":[],"unexpected":true");

        var error = Assert.Throws<PlanningFormatException>(() =>
            PrintJobPlanParser.Parse(json));

        Assert.Contains("schema", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsUnsupportedSchemaVersion()
    {
        var json = ValidJson().Replace(
            ""schemaVersion":"1.0"",
            ""schemaVersion":"2.0"");

        var error = Assert.Throws<PlanningFormatException>(() =>
            PrintJobPlanParser.Parse(json));

        Assert.Contains("schema.version", error.Message);
    }

    private static string ValidJson() =>
        """
        {
          "job": {
            "jobName": "4x6 photos",
            "sources": [{ "path": "C:/print/a.jpg", "copies": 2 }],
            "paper": {
              "widthMm": 210,
              "heightMm": 297,
              "orientation": "portrait"
            },
            "layout": {
              "mode": "grid",
              "itemWidthMm": 40,
              "itemHeightMm": 60,
              "gapMm": 3,
              "marginMm": 5,
              "allowRotate": true,
              "cutMarks": true,
              "fit": "cover"
            },
            "print": {
              "copies": 1,
              "colorMode": "color",
              "quality": "high",
              "duplex": "off"
            },
            "policy": { "preview": "smart" },
            "schemaVersion": "1.0"
          },
          "confidence": 0.96,
          "questions": [],
          "warnings": []
        }
        """;
}
