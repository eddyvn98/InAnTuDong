using System.IO;
using System.Net.Http;
using PrintAI.Domain;
using PrintAI.Planning;
using PrintAI.Rendering;
using PrintAI.SourceInspection;
using PrintAI.Spreadsheet;

namespace PrintAI.Desktop;

public sealed class DesktopPlannerSession
{
    private readonly HttpClient _httpClient = new();
    private ChatCompletionPlannerClient? _modelClient;
    private PrintPlanner? _planner;
    private GeneralPrintPlanner? _generalPlanner;
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
        _modelClient = new ChatCompletionPlannerClient(
            _httpClient,
            new ChatCompletionTransportOptions(uri, Model, apiKey));
        _planner = new PrintPlanner(_modelClient);
        _generalPlanner = new GeneralPrintPlanner(_modelClient);
        _spreadsheetPlanner = new SpreadsheetPrintPlanner(_modelClient);
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

    public async Task<SmartCollagePlan> PlanSmartCollageAsync(
        IReadOnlyList<DesktopPage> pages,
        IReadOnlyList<string> allowedTemplateIds,
        string? userInstruction = null,
        CancellationToken cancellationToken = default)
    {
        if (_modelClient is null)
        {
            throw new InvalidOperationException(
                "AI planner is not configured. Set endpoint and a vision-capable model first.");
        }

        if (pages.Count != 3)
            throw new ArgumentException("Smart Collage vision currently requires exactly three pages.");

        var images = new List<MultimodalImage>(3);

        for (var sourceIndex = 0; sourceIndex < pages.Count; sourceIndex++)
        {
            var page = pages[sourceIndex];
            var metadata = SourceInspector.Inspect(page.SourcePath);
            var png = SourceJobRenderer.RenderSourceThumbnailPng(
                page.SourcePath,
                page.SourcePageIndex,
                maxDimension: 768);

            images.Add(new MultimodalImage(
                SourceIndex: sourceIndex,
                DataUrl: $"data:image/png;base64,{Convert.ToBase64String(png)}",
                PixelWidth: metadata.PixelWidth,
                PixelHeight: metadata.PixelHeight));
        }

        return await new SmartCollagePlanner(_modelClient).PlanAsync(
            images,
            allowedTemplateIds,
            userInstruction,
            cancellationToken);
    }

    public async Task<DesktopGeneralPlanResult> PlanGeneralAsync(
        string request,
        IReadOnlyList<string> sourcePaths,
        SafetyMode mode,
        bool isVerifiedPrinterProfile,
        CancellationToken cancellationToken = default)
    {
        if (_generalPlanner is null)
        {
            throw new InvalidOperationException(
                "AI planner is not configured. Set endpoint and model first.");
        }

        if (sourcePaths.Count == 0)
            throw new ArgumentException("At least one source is required.", nameof(sourcePaths));

        var sources = sourcePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var metadata = SourceInspector.Inspect(path);
                return new PlanningSource(
                    Path: Path.GetFullPath(path),
                    Kind: metadata.Kind.ToString(),
                    PixelWidth: metadata.PixelWidth,
                    PixelHeight: metadata.PixelHeight,
                    PageCount: metadata.PageCount ?? 1,
                    Pages: metadata.Pages?
                        .Select(page => new SourcePageSizeSpec(
                            page.Page,
                            page.WidthMm,
                            page.HeightMm))
                        .ToArray());
            })
            .ToArray();

        var outcome = await _generalPlanner.PlanAsync(
            new PlanningRequest(request, sources),
            cancellationToken);

        var bound = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            outcome.Plan,
            sources);

        outcome = outcome with { Plan = bound };

        var decision = PrintPolicyEngine.Decide(
            outcome,
            new PolicyContext(
                Mode: mode,
                IsKnownRecipe: false,
                WasPreviouslyApproved: false,
                IsVerifiedPrinterProfile: isVerifiedPrinterProfile));

        return new(
            Outcome: outcome,
            Compiled: PrintPlanCompiler.Compile(bound),
            Decision: decision,
            Mode: mode);
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
            PageCount: metadata.PageCount,
            Pages: metadata.Pages?
                .Select(page => new SourcePageSizeSpec(
                    page.Page,
                    page.WidthMm,
                    page.HeightMm))
                .ToArray());

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

public sealed record DesktopGeneralPlanResult(
    GeneralPlanningOutcome Outcome,
    CompiledPrintPlan Compiled,
    PolicyDecision Decision,
    SafetyMode Mode);

public sealed record DesktopPlanResult(
    PlanningOutcome Outcome,
    PolicyDecision Decision,
    SafetyMode Mode);
