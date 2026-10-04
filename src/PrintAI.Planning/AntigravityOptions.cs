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
            FastEffort: Read("PRINTAI_AGY_FAST_EFFORT", "auto"),
            DeepEffort: Read("PRINTAI_AGY_DEEP_EFFORT", "auto"),
            EscalateBelowConfidence: ReadConfidence(),
            PrintTimeout: Read("PRINTAI_AGY_TIMEOUT", "2m"));
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CliPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(FastModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeepModel);

        ValidateEffort(FastModel, FastEffort, nameof(FastEffort));
        ValidateEffort(DeepModel, DeepEffort, nameof(DeepEffort));

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
        if (effort is not ("auto" or "low" or "medium" or "high"))
            throw new ArgumentException(
                "Antigravity effort must be auto, low, medium or high.",
                parameterName);
    }

    private static void ValidateEffort(string model, string effort, string parameterName)
    {
        ValidateEffort(effort, parameterName);
        var modelTier = ModelTier(model);
        if (effort != "auto" && modelTier is not null && effort != modelTier)
            throw new ArgumentException(
                $"Model '{model}' already encodes effort '{modelTier}'. Use effort '{modelTier}' or 'auto'.",
                parameterName);
    }

    public string? ResolveEffort(string model, string effort)
    {
        if (effort == "auto" || ModelTier(model) is not null)
            return null;
        return effort;
    }

    private static string? ModelTier(string model)
    {
        var suffix = model[(model.LastIndexOf('-') + 1)..];
        return suffix is "low" or "medium" or "high" ? suffix : null;
    }
}

public sealed record AntigravityInvocation(
    string CliPath,
    string Prompt,
    string Model,
    string? Effort,
    string? JsonSchema,
    string PrintTimeout,
    string? WorkingDirectory = null,
    bool AllowTools = false,
    bool AutoApproveSandboxTools = false);

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

public interface IAntigravityStreamingCommandRunner : IAntigravityCommandRunner
{
    Task<AntigravityCommandResult> RunStreamingAsync(
        AntigravityInvocation invocation,
        IProgress<PlannerProgressUpdate> progress,
        CancellationToken cancellationToken = default);
}

public sealed record AntigravityExecutionInfo(
    string Tier,
    string Model,
    bool Escalated,
    string Reason,
    double DurationMilliseconds = 0);
