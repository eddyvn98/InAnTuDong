using PrintAI.Domain;

namespace PrintAI.Planning;

public enum SafetyMode
{
    Safe,
    Smart,
    Auto
}

public enum PolicyDecisionKind
{
    Direct,
    PreviewRequired,
    QuestionRequired,
    Rejected
}

public sealed record PolicyContext(
    SafetyMode Mode,
    bool IsKnownRecipe = false,
    bool WasPreviouslyApproved = false,
    bool IsVerifiedPrinterProfile = false);

public sealed record PolicyDecision(
    PolicyDecisionKind Kind,
    string Reason);

public static class PrintPolicyEngine
{
    public static PolicyDecision Decide(
        PlanningOutcome outcome,
        PolicyContext context)
    {
        var validation = PrintJobValidator.Validate(outcome.Job);
        if (!validation.IsValid)
            return new(PolicyDecisionKind.Rejected, "PrintJobSpec validation failed.");

        return DecideCore(
            outcome.Confidence,
            outcome.Questions,
            outcome.Warnings,
            context);
    }

    public static PolicyDecision Decide(
        GeneralPlanningOutcome outcome,
        PolicyContext context)
    {
        var validation = PrintPlanValidator.Validate(outcome.Plan);
        if (!validation.IsValid)
            return new(PolicyDecisionKind.Rejected, "PrintPlan validation failed.");

        return DecideCore(
            outcome.Confidence,
            outcome.Questions,
            outcome.Warnings,
            context);
    }

    private static PolicyDecision DecideCore(
        double confidence,
        IReadOnlyList<string> questions,
        IReadOnlyList<string> warnings,
        PolicyContext context)
    {
        if (questions.Count > 0)
            return new(
                PolicyDecisionKind.QuestionRequired,
                "Planner reported material ambiguity that requires user input.");

        if (context.Mode == SafetyMode.Safe)
            return new(
                PolicyDecisionKind.PreviewRequired,
                "Safe mode always requires preview approval.");

        if (!context.IsVerifiedPrinterProfile)
            return new(
                PolicyDecisionKind.PreviewRequired,
                "Printer profile has not been physically verified.");

        if (confidence < 0.90 || warnings.Count > 0)
            return new(
                PolicyDecisionKind.PreviewRequired,
                "Planner confidence or warnings require preview.");

        if (context.Mode == SafetyMode.Smart &&
            !context.IsKnownRecipe &&
            !context.WasPreviouslyApproved)
        {
            return new(
                PolicyDecisionKind.PreviewRequired,
                "Smart mode previews jobs that are not known or previously approved.");
        }

        return new(
            PolicyDecisionKind.Direct,
            context.Mode == SafetyMode.Auto
                ? "Auto mode was explicitly selected and the job passed all gates."
                : "Known Smart-mode job passed all gates.");
    }
}
