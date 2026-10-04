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

    public async Task<LocalRequestRoute> RouteAsync(
        string userRequest,
        IReadOnlyList<UploadedSourceView> sources,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null)
    {
        if (_client is null)
            throw new InvalidOperationException("AGY chưa sẵn sàng.");

        var sourceSummary = sources.Count == 0
            ? "(no uploaded source)"
            : string.Join("\n", sources.Select(source =>
                $"- {source.FileName} | {source.Kind} | {source.PageCount} page(s)"));

        var instruction = """
            You route one request inside a local AI document/printing app.
            Return one compact JSON object only:
            {"route":"print|artifact|artifactThenPrint","printRequest":"string","reason":"string"}

            Use print when the selected files/content only need print layout, page selection,
            scaling, copies, duplex, imposition, poster, labels, collage placement, or printing.

            Use artifact when the user wants the underlying content/file changed or created before
            printing: edit/reformat/rewrite an Office document, alter document structure/content,
            manipulate an image itself, modify a PDF itself, or create a native/editable artifact.

            Use artifactThenPrint when both are requested in the same instruction.
            printRequest must contain only the printing part needed after artifact editing.
            For artifact-only requests printRequest must be an empty string.
            Do not route ordinary print layout changes to artifact merely because layout is mentioned.
            """;

        var raw = await _client.CompleteUnstructuredAsync(
            instruction,
            $"REQUEST:\n{userRequest.Trim()}\n\nSOURCES:\n{sourceSummary}",
            cancellationToken,
            progress);

        try
        {
            var jsonText = raw.Trim();
            var objectStart = jsonText.IndexOf('{');
            var objectEnd = jsonText.LastIndexOf('}');
            if (objectStart >= 0 && objectEnd > objectStart)
                jsonText = jsonText[objectStart..(objectEnd + 1)];

            using var json = System.Text.Json.JsonDocument.Parse(jsonText);
            var root = json.RootElement;
            var route = root.GetProperty("route").GetString() ?? "print";
            if (route is not ("print" or "artifact" or "artifactThenPrint"))
                throw new FormatException("Unknown route.");

            return new(
                route,
                root.TryGetProperty("printRequest", out var printRequest)
                    ? printRequest.GetString() ?? ""
                    : "",
                root.TryGetProperty("reason", out var reason)
                    ? reason.GetString() ?? ""
                    : "");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or FormatException)
        {
            return new("print", userRequest.Trim(), "router-fallback");
        }
    }

    public Task<string> ExecuteArtifactAsync(
        string userRequest,
        string workspace,
        string sourceManifest,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null)
    {
        if (_client is null)
            throw new InvalidOperationException("AGY chưa sẵn sàng.");

        var instruction = """
            You are the artifact-editing worker for PrintAI.
            The current working directory is a temporary isolated task workspace.
            User-selected files are under input/. Never access paths outside this workspace.
            Put every final deliverable under output/. Keep the original input files untouched.
            You may create one or more output files when useful.
            Preserve editable/native formats when practical (for example DOCX stays DOCX).
            Produce only final deliverables. Do not also create a duplicate PDF preview/export
            unless the user explicitly requests it; PrintAI creates preview representations itself.
            Use installed local document/image tools when needed.
            Do not print and do not call a printer or spooler.
            Do not merely describe steps: perform the requested file work.
            """;

        return _client.ExecuteArtifactTaskAsync(
            instruction,
            $"USER REQUEST:\n{userRequest.Trim()}\n\nINPUT MANIFEST:\n{sourceManifest}\n\nFinal deliverables must be placed under output/.",
            workspace,
            cancellationToken,
            progress);
    }

    public Task<GeneralPlanningOutcome> PlanAsync(
        PlanningRequest request,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null) =>
        (_planner ?? throw new InvalidOperationException("AGY chưa sẵn sàng."))
        .PlanAsync(request, cancellationToken, progress);
}

public sealed record LocalRequestRoute(string Route, string PrintRequest, string Reason);

public sealed record LocalPlannerReadiness(bool IsReady, string? Models, string Message);
