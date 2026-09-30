using PrintAI.Planning;

namespace PrintAI.Web;

public sealed class LocalWorkflowPlanner
{
    private readonly AntigravityPlannerClient? _client;
    private readonly PrintPlanner? _planner;
    private string? _lastTier;

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
        _planner = new PrintPlanner(_client);
        Readiness = new(true,
            $"A4 đen trắng có sẵn · AGY {options.FastModel} → {options.DeepModel} khi cần",
            "AGY đã sẵn sàng.");
    }

    public LocalPlannerReadiness Readiness { get; }
    public string? LastTier => _lastTier ?? _client?.LastExecution?.Tier;

    public Task<PlanningOutcome> PlanAsync(
        PlanningRequest request,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null) =>
        PlanCoreAsync(request, cancellationToken, progress);

    private async Task<PlanningOutcome> PlanCoreAsync(
        PlanningRequest request,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress)
    {
        if (SimpleA4MonochromePlanner.TryPlan(request, out var quickPlan))
        {
            _lastTier = "fast-rules";
            progress?.Report(new(
                "status",
                "Yêu cầu khớp luồng A4 đen trắng có sẵn; đang xử lý nhanh, không cần gọi AGY."));
            return quickPlan;
        }

        _lastTier = null;
        return await (_planner ?? throw new InvalidOperationException("AGY chưa sẵn sàng."))
            .PlanAsync(request, cancellationToken, progress);
    }
}

public sealed record LocalPlannerReadiness(bool IsReady, string? Models, string Message);
