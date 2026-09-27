using System.IO;
using System.Net.Http;
using PrintAI.Domain;
using PrintAI.Planning;
using PrintAI.SourceInspection;
using PrintAI.Spreadsheet;

namespace PrintAI.Desktop;

public sealed class DesktopPlannerSession
{
    private readonly HttpClient _httpClient = new();
    private PrintPlanner? _planner;
    private SpreadsheetPrintPlanner? _spreadsheetPlanner;

    public string? Endpoint { get; private set; }
    public string? Model { get; private set; }
    public bool IsConfigured => _planner is not null;

    public void Configure(string endpoint, string model, string? apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("AI endpoint must be an absolute HTTP/HTTPS URL.");
        }

        Endpoint = uri.ToString();
        Model = model.Trim();
        var client = new ChatCompletionPlannerClient(
            _httpClient,
            new ChatCompletionTransportOptions(uri, Model, apiKey));

        _planner = new PrintPlanner(client);
        _spreadsheetPlanner = new SpreadsheetPrintPlanner(client);
    }

    public bool ConfigureFromEnvironment()
    {
        var endpoint = Environment.GetEnvironmentVariable("PRINTAI_AI_ENDPOINT");
        var model = Environment.GetEnvironmentVariable("PRINTAI_AI_MODEL");

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        Configure(
            endpoint,
            model,
            Environment.GetEnvironmentVariable("PRINTAI_AI_API_KEY"));

        return true;
    }


    public async Task<SpreadsheetPlanningOutcome> PlanSpreadsheetAsync(
        string request,
        SpreadsheetWorkbookProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (_spreadsheetPlanner is null)
        {
            throw new InvalidOperationException(
                "AI planner is not configured. Set endpoint and model first.");
        }

        return await _spreadsheetPlanner.PlanAsync(
            request,
            profile,
            cancellationToken);
    }

    public async Task<DesktopPlanResult> PlanAsync(
        string request,
        DesktopPage page,
        SafetyMode mode,
        bool isVerifiedPrinterProfile,
        CancellationToken cancellationToken = default)
    {
        if (_planner is null)
        {
            throw new InvalidOperationException(
                "AI planner is not configured. Set endpoint and model first.");
        }

        var metadata = SourceInspector.Inspect(page.SourcePath);
        var source = new PlanningSource(
            Path: Path.GetFullPath(page.SourcePath),
            Kind: metadata.Kind.ToString(),
            PixelWidth: metadata.PixelWidth,
            PixelHeight: metadata.PixelHeight,
            PageCount: metadata.PageCount);

        var outcome = await _planner.PlanAsync(
            new PlanningRequest(request, [source]),
            cancellationToken);

        var bound = PlannerSourceBinder.BindToAllowedSources(
            outcome.Job,
            [page.SourcePath]);

        outcome = outcome with { Job = bound };

        var decision = PrintPolicyEngine.Decide(
            outcome,
            new PolicyContext(
                Mode: mode,
                IsKnownRecipe: false,
                WasPreviouslyApproved: false,
                IsVerifiedPrinterProfile: isVerifiedPrinterProfile));

        return new(outcome, decision, mode);
    }
}

public sealed record DesktopPlanResult(
    PlanningOutcome Outcome,
    PolicyDecision Decision,
    SafetyMode Mode);
