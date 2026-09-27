using PrintAI.Domain;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintPolicyEngineTests
{
    [Fact]
    public void Safe_AlwaysRequiresPreview()
    {
        var result = PrintPolicyEngine.Decide(
            Outcome(),
            new PolicyContext(
                SafetyMode.Safe,
                IsVerifiedPrinterProfile: true));

        Assert.Equal(PolicyDecisionKind.PreviewRequired, result.Kind);
    }

    [Fact]
    public void Smart_DirectOnlyForKnownVerifiedHighConfidenceJob()
    {
        var result = PrintPolicyEngine.Decide(
            Outcome(),
            new PolicyContext(
                SafetyMode.Smart,
                IsKnownRecipe: true,
                IsVerifiedPrinterProfile: true));

        Assert.Equal(PolicyDecisionKind.Direct, result.Kind);
    }

    [Fact]
    public void Smart_PreviewsUnknownJob()
    {
        var result = PrintPolicyEngine.Decide(
            Outcome(),
            new PolicyContext(
                SafetyMode.Smart,
                IsVerifiedPrinterProfile: true));

        Assert.Equal(PolicyDecisionKind.PreviewRequired, result.Kind);
    }

    [Fact]
    public void Auto_StillStopsForPlannerQuestions()
    {
        var outcome = Outcome() with { Questions = ["Kích thước ảnh mong muốn?"] };

        var result = PrintPolicyEngine.Decide(
            outcome,
            new PolicyContext(
                SafetyMode.Auto,
                IsVerifiedPrinterProfile: true));

        Assert.Equal(PolicyDecisionKind.QuestionRequired, result.Kind);
    }

    [Fact]
    public void Auto_DirectWhenExplicitAndAllGatesPass()
    {
        var result = PrintPolicyEngine.Decide(
            Outcome(),
            new PolicyContext(
                SafetyMode.Auto,
                IsVerifiedPrinterProfile: true));

        Assert.Equal(PolicyDecisionKind.Direct, result.Kind);
    }

    private static PlanningOutcome Outcome() =>
        new(
            new PrintJobSpec(
                "test",
                [new SourceSpec("C:/a.png")],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.Grid,
                    200,
                    287,
                    MarginMm: 5,
                    AllowRotate: false),
                new PrintSettings(),
                new PolicySpec(),
                "1.0"),
            0.96,
            [],
            [],
            "{}");
}
