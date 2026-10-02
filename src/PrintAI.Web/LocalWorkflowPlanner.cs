using PrintAI.Planning;

namespace PrintAI.Web;

public sealed class LocalWorkflowPlanner
{
    private readonly AntigravityPlannerClient? _client;
    private readonly GeneralPrintPlanner? _planner;
    private readonly IPlannerModelClient? _modelClient;
    private readonly SourceCapabilityRegistry _capabilities = new();

    public LocalWorkflowPlanner()
    {
        var cliPath = AntigravityLocator.Resolve();
        if (cliPath is null)
        {
            Readiness = new(false, null, "Không tìm thấy AGY CLI trong PATH.");
            return;
        }

        var options = AntigravityPlannerOptions.FromEnvironment(cliPath);
        _client = new AntigravityPlannerClient(options);
        _modelClient = _client;
        _planner = new GeneralPrintPlanner(_client);
        Readiness = new(true, $"{options.FastModel} → {options.DeepModel}", "AGY đã sẵn sàng.");
    }

    public LocalPlannerReadiness Readiness { get; }
    public string? LastTier => _client?.LastExecution?.Tier;

    public Task<string> CreateSourceAsync(
        string userRequest,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null)
    {
        if (_modelClient is null)
            throw new InvalidOperationException("AGY chưa sẵn sàng.");

        var instruction = """
            You are the source-creation router for an AI printing system.
            Available and unavailable capabilities are listed below.
            Never pretend an unavailable capability exists.
            Prefer an available capability that still fulfills the user's real goal.
            If no available capability can fulfill the request, return exactly [NEEDS_TOOL:<capability-id>] followed by a short explanation.
            
            CAPABILITIES:
            """ + "\n" + _capabilities.DescribeForModel() + "\n\n" + """
            
            When text-pdf can fulfill the request, create printable source content.
            You create printable source content when the user has not supplied a file.
            Return plain UTF-8 text only, not JSON or markdown fences.
            Create the actual content to place on an A4 document, not instructions about how to create it.
            Preserve the user's language. Keep content concise enough to print clearly.
            You may create notices, letters, simple forms, checklists, labels, signs, study sheets, and other text-first printable material.
            Do not return markdown fences.
            """;

        return _modelClient.CompleteAsync(
            new PlannerModelRequest(instruction, userRequest.Trim(), progress),
            cancellationToken);
    }

    public Task<GeneralPlanningOutcome> PlanAsync(
        PlanningRequest request,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null) =>
        (_planner ?? throw new InvalidOperationException("AGY chưa sẵn sàng."))
        .PlanAsync(request, cancellationToken, progress);
}

public sealed record LocalPlannerReadiness(bool IsReady, string? Models, string Message);
