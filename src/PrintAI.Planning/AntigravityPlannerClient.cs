using System.Text.Json;

namespace PrintAI.Planning;

public sealed class AntigravityPlannerClient : IPlannerModelClient
{
    private const string FastDirective =
        """
        FAST PASS:
        Produce the exact JSON requested by the PrintAI instruction below.
        Be concise. Do not explain. Do not call tools, shell commands, or edit files.
        Use only the supplied request and inspected metadata.
        If the request is materially ambiguous, ask a concise question instead of guessing.
        Confidence must reflect how certain the proposed print intent is.
        """;

    private const string DeepDirective =
        """
        DEEP PASS:
        Re-evaluate the PrintAI request carefully and produce the exact requested JSON.
        Do not explain. Do not call tools, shell commands, or edit files.
        Use only the supplied request and inspected metadata.
        Resolve complexity, but do not guess missing material facts; ask a concise question instead.
        """;

    private readonly IAntigravityCommandRunner _runner;
    private readonly AntigravityPlannerOptions _options;

    public AntigravityPlannerClient(
        AntigravityPlannerOptions options,
        IAntigravityCommandRunner? runner = null)
    {
        options.Validate();
        _options = options;
        _runner = runner ?? new AntigravityProcessRunner();
    }

    public AntigravityExecutionInfo? LastExecution { get; private set; }

    public async Task<string> CompleteAsync(
        PlannerModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fast;
        try
        {
            fast = await RunTierAsync(
                request,
                _options.FastModel,
                _options.FastEffort,
                FastDirective,
                cancellationToken);
        }
        catch (PlannerTransportException)
        {
            var deepAfterFailure = await RunTierAsync(
                request,
                _options.DeepModel,
                _options.DeepEffort,
                DeepDirective,
                cancellationToken);

            EnsurePlannerPayload(deepAfterFailure, request);

            LastExecution = new(
                "deep",
                _options.DeepModel,
                Escalated: true,
                Reason: "fast-transport-failure");

            return deepAfterFailure;
        }

        var inspection = Inspect(fast, request);

        if (!inspection.ShouldEscalate)
        {
            LastExecution = new(
                "fast",
                _options.FastModel,
                Escalated: false,
                Reason: inspection.Reason);

            return fast;
        }

        var deep = await RunTierAsync(
            request,
            _options.DeepModel,
            _options.DeepEffort,
            DeepDirective,
            cancellationToken);

        EnsurePlannerPayload(deep, request);

        LastExecution = new(
            "deep",
            _options.DeepModel,
            Escalated: true,
            Reason: inspection.Reason);

        return deep;
    }

    private async Task<string> RunTierAsync(
        PlannerModelRequest request,
        string model,
        string effort,
        string directive,
        CancellationToken cancellationToken)
    {
        var prompt =
            $"{directive}\n\nPRINTAI INSTRUCTION:\n{request.SystemInstruction}" +
            $"\n\nINPUT:\n{request.UserPayload}";

        var invocation = new AntigravityInvocation(
            _options.CliPath,
            prompt,
            model,
            _options.ResolveEffort(model, effort),
            AntigravityPlannerSchema.Resolve(request.SystemInstruction),
            _options.PrintTimeout);

        var result = await _runner.RunAsync(
            invocation,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new PlannerTransportException(
                $"Antigravity CLI exited with code {result.ExitCode}.",
                result.StandardError);
        }

        return ExtractStructuredOutput(
            result.StandardOutput,
            result.StandardError);
    }

    private PlannerInspection Inspect(
        string payload,
        PlannerModelRequest request)
    {
        try
        {
            if (AntigravityPlannerSchema.IsGeneralPlan(request.SystemInstruction))
            {
                var outcome = GeneralPrintPlanParser.Parse(payload);
                if (outcome.Questions.Count > 0)
                    return new(false, "clarification-required");

                return outcome.Confidence < _options.EscalateBelowConfidence
                    ? new(true, "low-confidence")
                    : new(false, "fast-valid");
            }

            if (AntigravityPlannerSchema.IsJob(request.SystemInstruction))
            {
                var outcome = PrintJobPlanParser.Parse(payload);
                if (outcome.Questions.Count > 0)
                    return new(false, "clarification-required");

                return outcome.Confidence < _options.EscalateBelowConfidence
                    ? new(true, "low-confidence")
                    : new(false, "fast-valid");
            }

            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return new(true, "non-object-response");

            if (json.RootElement.TryGetProperty(
                    "confidence",
                    out var confidence) &&
                confidence.ValueKind == JsonValueKind.Number &&
                confidence.TryGetDouble(out var value) &&
                value < _options.EscalateBelowConfidence)
            {
                return new(true, "low-confidence");
            }

            return new(false, "fast-valid");
        }
        catch (Exception ex) when (
            ex is JsonException or
            PlanningFormatException)
        {
            return new(true, "fast-invalid");
        }
    }

    private static void EnsurePlannerPayload(
        string payload,
        PlannerModelRequest request)
    {
        if (AntigravityPlannerSchema.IsGeneralPlan(request.SystemInstruction))
        {
            _ = GeneralPrintPlanParser.Parse(payload);
            return;
        }

        if (AntigravityPlannerSchema.IsJob(request.SystemInstruction))
            _ = PrintJobPlanParser.Parse(payload);
    }

    private static string ExtractStructuredOutput(
        string stdout,
        string stderr)
    {
        try
        {
            using var envelope = JsonDocument.Parse(stdout);
            var root = envelope.RootElement;

            if (root.TryGetProperty("status", out var status) &&
                !string.Equals(
                    status.GetString(),
                    "SUCCESS",
                    StringComparison.OrdinalIgnoreCase))
            {
                var error = root.TryGetProperty("error", out var errorNode)
                    ? errorNode.GetString()
                    : stderr;

                throw new PlannerTransportException(
                    "Antigravity CLI returned a non-success status.",
                    error);
            }

            if (root.TryGetProperty(
                    "structured_output",
                    out var structured) &&
                structured.ValueKind is not JsonValueKind.Null and
                    not JsonValueKind.Undefined)
            {
                return structured.GetRawText();
            }

            if (root.TryGetProperty("response", out var response) &&
                response.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(response.GetString()))
            {
                return response.GetString()!.Trim();
            }
        }
        catch (JsonException ex)
        {
            throw new PlannerTransportException(
                "Antigravity CLI returned an invalid JSON envelope.",
                stdout,
                ex);
        }

        throw new PlannerTransportException(
            "Antigravity CLI returned no planner payload.",
            string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    private sealed record PlannerInspection(
        bool ShouldEscalate,
        string Reason);
}
