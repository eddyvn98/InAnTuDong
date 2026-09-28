using System.IO;
using PrintAI.Domain;
using PrintAI.Planning;
using PrintAI.Rendering;
using PrintAI.SourceInspection;
using PrintAI.Spreadsheet;

namespace PrintAI.Desktop;

public sealed class DesktopPlannerSession
{
    private AntigravityPlannerClient? _antigravityClient;
    private PrintPlanner? _planner;
    private GeneralPrintPlanner? _generalPlanner;
    private SpreadsheetPrintPlanner? _spreadsheetPlanner;

    public string? Endpoint { get; private set; }
    public string? Model { get; private set; }
    public string? PlannerTier => _antigravityClient?.LastExecution?.Tier;
    public bool IsConfigured => _planner is not null;

    public bool ConfigureAntigravity(string? cliPath = null)
    {
        var resolved = AntigravityLocator.Resolve(cliPath);
        if (resolved is null)
            return false;

        var options = AntigravityPlannerOptions.FromEnvironment(resolved);
        var client = new AntigravityPlannerClient(options);

        Endpoint = resolved;
        Model = $"{options.FastModel} -> {options.DeepModel}";
        _antigravityClient = client;
        _planner = new PrintPlanner(client);
        _generalPlanner = new GeneralPrintPlanner(client);
        _spreadsheetPlanner = new SpreadsheetPrintPlanner(client);
        return true;
    }

    public bool ConfigureFromEnvironment() =>
        ConfigureAntigravity();



    public async Task<SpreadsheetPlanningOutcome> PlanSpreadsheetAsync(
        string request,
        SpreadsheetWorkbookProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (_spreadsheetPlanner is null)
        {
            throw new InvalidOperationException(
                "Antigravity planner is not ready. Install/login to AGY or set PRINTAI_AGY_PATH.");
        }

        return await _spreadsheetPlanner.PlanAsync(
            request,
            profile,
            cancellationToken);
    }

    public Task<SmartCollagePlan> PlanSmartCollageAsync(
        IReadOnlyList<DesktopPage> pages,
        IReadOnlyList<string> allowedTemplateIds,
        string? userInstruction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Smart Collage vision is not yet migrated to the Antigravity CLI. " +
            "Use deterministic collage templates until the AGY local-image path is verified.");
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
                "Antigravity planner is not ready. Install/login to AGY or set PRINTAI_AGY_PATH.");
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
                "Antigravity planner is not ready. Install/login to AGY or set PRINTAI_AGY_PATH.");
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
