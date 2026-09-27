using PrintAI.Domain;
using PrintAI.Planning;
using PrintAI.SourceInspection;

namespace PrintAI.Desktop;

public sealed class DesktopPlanningController : IDisposable
{
    private readonly PlannerGatewayClient? _client;
    private readonly PrintPlanner? _planner;

    public DesktopPlanningController()
    {
        var config = PlannerGatewayConfiguration.FromEnvironment();
        if (config is null)
            return;

        _client = new PlannerGatewayClient(
            config.Endpoint,
            config.BearerToken);
        _planner = new PrintPlanner(_client);
    }

    public bool IsAvailable => _planner is not null;
    public DesktopPlanState? Current { get; private set; }
    public PlanningOutcome? Outcome { get; private set; }

    public async Task<DesktopPlanState> PlanAsync(
        string userRequest,
        IReadOnlyList<string> sourcePaths,
        string? selectedPrinter,
        SafetyMode mode,
        CancellationToken cancellationToken = default)
    {
        if (_planner is null)
        {
            return Current = new(
                Available: false,
                Error: "Chưa cấu hình PRINTAI_PLANNER_URL.");
        }

        if (sourcePaths.Count == 0)
        {
            return Current = new(
                Available: true,
                Error: "Hãy chọn ít nhất một file trước khi Plan.");
        }

        try
        {
            var sources = sourcePaths
                .Select(ToPlanningSource)
                .ToArray();

            Outcome = await _planner.PlanAsync(
                new PlanningRequest(userRequest, sources),
                cancellationToken);

            var verifiedPrinter =
                !string.IsNullOrWhiteSpace(selectedPrinter) &&
                selectedPrinter.Contains(
                    "L3310",
                    StringComparison.OrdinalIgnoreCase);

            var decision = PrintPolicyEngine.Decide(
                Outcome,
                new PolicyContext(
                    mode,
                    IsKnownRecipe: false,
                    WasPreviouslyApproved: false,
                    IsVerifiedPrinterProfile: verifiedPrinter));

            return Current = new(
                Available: true,
                Confidence: Outcome.Confidence,
                Decision: decision.Kind.ToString(),
                DecisionReason: decision.Reason,
                JobName: Outcome.Job.JobName,
                Layout: DescribeLayout(Outcome.Job),
                Questions: Outcome.Questions,
                Warnings: Outcome.Warnings,
                CanApplyPreview: CanApplySingleSourcePreview(
                    Outcome.Job,
                    sourcePaths),
                Error: null);
        }
        catch (Exception ex)
        {
            Outcome = null;
            return Current = new(
                Available: true,
                Error: ex.Message);
        }
    }

    public void Clear()
    {
        Outcome = null;
        Current = null;
    }

    public PrintJobSpec? ResolveJobForSource(string sourcePath)
    {
        var job = Outcome?.Job;
        if (job is null || job.Sources.Count != 1)
            return null;

        return PathsEqual(job.Sources[0].Path, sourcePath)
            ? job
            : null;
    }

    public void Dispose() => _client?.Dispose();

    private static PlanningSource ToPlanningSource(string path)
    {
        var metadata = SourceInspector.Inspect(path);
        return new(
            Path: path,
            Kind: metadata.Kind.ToString(),
            PixelWidth: metadata.PixelWidth,
            PixelHeight: metadata.PixelHeight,
            PageCount: metadata.PageCount);
    }

    private static string DescribeLayout(PrintJobSpec job) =>
        $"{job.Layout.Mode} · {job.Layout.ItemWidthMm:0.##}×{job.Layout.ItemHeightMm:0.##} mm · " +
        $"{job.Layout.Fit} · copies {job.Print.Copies}";

    private static bool CanApplySingleSourcePreview(
        PrintJobSpec job,
        IReadOnlyList<string> sourcePaths) =>
        job.Sources.Count == 1 &&
        sourcePaths.Any(path => PathsEqual(path, job.Sources[0].Path));

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

public sealed record DesktopPlanState(
    bool Available,
    double? Confidence = null,
    string? Decision = null,
    string? DecisionReason = null,
    string? JobName = null,
    string? Layout = null,
    IReadOnlyList<string>? Questions = null,
    IReadOnlyList<string>? Warnings = null,
    bool CanApplyPreview = false,
    string? Error = null);
