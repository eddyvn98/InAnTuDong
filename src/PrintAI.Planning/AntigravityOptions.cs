using System.Globalization;

namespace PrintAI.Planning;

public sealed record AntigravityPlannerOptions(
    string CliPath,
    string FastModel,
    string DeepModel,
    string FastEffort = "low",
    string DeepEffort = "high",
    double EscalateBelowConfidence = 0.80,
    string PrintTimeout = "2m")
{
    public static AntigravityPlannerOptions FromEnvironment(string cliPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliPath);

        return new(
            CliPath: cliPath,
            FastModel: Read("PRINTAI_AGY_FAST_MODEL", "gemini-3.8-flash-medium"),
            DeepModel: Read("PRINTAI_AGY_DEEP_MODEL", "gemini-3.8-flash-high"),
            FastEffort: Read("PRINTAI_AGY_FAST_EFFORT", "low"),
            DeepEffort: Read("PRINTAI_AGY_DEEP_EFFORT", "high"),
            EscalateBelowConfidence: ReadConfidence(),
            PrintTimeout: Read("PRINTAI_AGY_TIMEOUT", "2m"));
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CliPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(FastModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeepModel);

        ValidateEffort(FastEffort, nameof(FastEffort));
        ValidateEffort(DeepEffort, nameof(DeepEffort));

        if (EscalateBelowConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(EscalateBelowConfidence),
                "Escalation confidence must be between 0 and 1.");

        ArgumentException.ThrowIfNullOrWhiteSpace(PrintTimeout);
    }

    private static string Read(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static double ReadConfidence()
    {
        var raw = Environment.GetEnvironmentVariable(
            "PRINTAI_AGY_ESCALATE_BELOW");

        return double.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0.80;
    }

    private static void ValidateEffort(string effort, string parameterName)
    {
        if (effort is not ("low" or "medium" or "high"))
            throw new ArgumentException(
                "Antigravity effort must be low, medium or high.",
                parameterName);
    }
}

public sealed record AntigravityInvocation(
    string CliPath,
    string Prompt,
    string Model,
    string Effort,
    string JsonSchema,
    string PrintTimeout);

public sealed record AntigravityCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IAntigravityCommandRunner
{
    Task<AntigravityCommandResult> RunAsync(
        AntigravityInvocation invocation,
        CancellationToken cancellationToken = default);
}

public sealed record AntigravityExecutionInfo(
    string Tier,
    string Model,
    bool Escalated,
    string Reason);
