using PrintAI.Planning;

namespace PrintAI.Web;

public sealed class LocalWorkflowPlanner
{
    private readonly AntigravityPlannerClient? _client;
    private readonly GeneralPrintPlanner? _planner;

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
        _planner = new GeneralPrintPlanner(_client);
        Readiness = new(true, $"{options.FastModel} → {options.DeepModel}", "AGY đã sẵn sàng.");
    }

    public LocalPlannerReadiness Readiness { get; }
    public string? LastTier => _client?.LastExecution?.Tier;

    public Task<GeneralPlanningOutcome> PlanAsync(
        PlanningRequest request,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null) =>
        (_planner ?? throw new InvalidOperationException("AGY chưa sẵn sàng."))
        .PlanAsync(request, cancellationToken, progress);
}

public sealed record LocalPlannerReadiness(bool IsReady, string? Models, string Message);
