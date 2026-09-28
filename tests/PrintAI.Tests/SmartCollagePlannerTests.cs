using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class SmartCollagePlannerTests
{
    [Fact]
    public void Parse_AcceptsStrictThreeImageProposal()
    {
        const string json =
            """
            {
              "analysis": [
                {"sourceIndex":0,"orientation":"portrait","importance":0.9,"subjectX":0.5,"subjectY":0.4,"notes":"main portrait"},
                {"sourceIndex":1,"orientation":"landscape","importance":0.7,"subjectX":0.6,"subjectY":0.5,"notes":"group"},
                {"sourceIndex":2,"orientation":"square","importance":0.6,"subjectX":0.5,"subjectY":0.5,"notes":"close up"}
              ],
              "candidates": [
                {
                  "templateId":"hero-left-two-right",
                  "confidence":0.92,
                  "reason":"Source 0 is the strongest portrait.",
                  "frames":[
                    {"frameIndex":0,"sourceIndex":0,"scale":1.15,"offsetX":0.0,"offsetY":-0.1},
                    {"frameIndex":1,"sourceIndex":1,"scale":1.0,"offsetX":0.1,"offsetY":0.0},
                    {"frameIndex":2,"sourceIndex":2,"scale":1.1,"offsetX":0.0,"offsetY":0.0}
                  ]
                }
              ]
            }
            """;

        var plan = SmartCollagePlanner.Parse(
            json,
            ["hero-left-two-right", "hero-top-two-bottom"]);

        Assert.Equal(3, plan.Analysis.Count);
        Assert.Single(plan.Candidates);
        Assert.Equal("hero-left-two-right", plan.Candidates[0].TemplateId);
        Assert.Equal(0.92, plan.Candidates[0].Confidence, 3);
    }

    [Fact]
    public void Parse_RejectsInventedTemplate()
    {
        const string json =
            """
            {
              "analysis": [
                {"sourceIndex":0,"orientation":"portrait","importance":0.9,"subjectX":0.5,"subjectY":0.4},
                {"sourceIndex":1,"orientation":"portrait","importance":0.7,"subjectX":0.5,"subjectY":0.5},
                {"sourceIndex":2,"orientation":"portrait","importance":0.6,"subjectX":0.5,"subjectY":0.5}
              ],
              "candidates": [
                {
                  "templateId":"made-up-template",
                  "confidence":0.8,
                  "reason":"invalid",
                  "frames":[
                    {"frameIndex":0,"sourceIndex":0,"scale":1.0,"offsetX":0.0,"offsetY":0.0},
                    {"frameIndex":1,"sourceIndex":1,"scale":1.0,"offsetX":0.0,"offsetY":0.0},
                    {"frameIndex":2,"sourceIndex":2,"scale":1.0,"offsetX":0.0,"offsetY":0.0}
                  ]
                }
              ]
            }
            """;

        Assert.Throws<PlanningFormatException>(() =>
            SmartCollagePlanner.Parse(json, ["hero-left-two-right"]));
    }

    [Fact]
    public void Parse_RejectsDuplicateSourceAssignment()
    {
        const string json =
            """
            {
              "analysis": [
                {"sourceIndex":0,"orientation":"portrait","importance":0.9,"subjectX":0.5,"subjectY":0.4},
                {"sourceIndex":1,"orientation":"portrait","importance":0.7,"subjectX":0.5,"subjectY":0.5},
                {"sourceIndex":2,"orientation":"portrait","importance":0.6,"subjectX":0.5,"subjectY":0.5}
              ],
              "candidates": [
                {
                  "templateId":"hero-left-two-right",
                  "confidence":0.8,
                  "reason":"invalid",
                  "frames":[
                    {"frameIndex":0,"sourceIndex":0,"scale":1.0,"offsetX":0.0,"offsetY":0.0},
                    {"frameIndex":1,"sourceIndex":0,"scale":1.0,"offsetX":0.0,"offsetY":0.0},
                    {"frameIndex":2,"sourceIndex":2,"scale":1.0,"offsetX":0.0,"offsetY":0.0}
                  ]
                }
              ]
            }
            """;

        Assert.Throws<PlanningFormatException>(() =>
            SmartCollagePlanner.Parse(json, ["hero-left-two-right"]));
    }
}
