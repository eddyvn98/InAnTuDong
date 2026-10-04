using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Planning;
using PrintAI.SourceInspection;
using System.Diagnostics;

namespace PrintAI.Web;

public sealed partial class LocalWorkflowSession
{
    public async Task<LocalPlanResult> PlanJobAsync(
        LocalPlanRequest request,
        LocalWorkflowPlanner planner,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null)
    {
        var elapsed = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.UserRequest) || request.UserRequest.Length > 2000)
            throw new LocalWorkflowException("Yêu cầu cần có nội dung và tối đa 2.000 ký tự.");
        if (request.SourceIds is null || request.SourceIds.Count > MaxFiles ||
            request.SourceIds.Distinct().Count() != request.SourceIds.Count)
            throw new LocalWorkflowException("Danh sách tệp không hợp lệ.");

        UploadedSourceView[] routeSources;
        lock (_sync)
        {
            routeSources = request.SourceIds.Select(id =>
                _sources.TryGetValue(id, out var source)
                    ? source.View
                    : throw new LocalWorkflowException("Một tệp đã chọn không còn trong phiên.")).ToArray();
        }

        var route = await planner.RouteAsync(
            request.UserRequest.Trim(),
            routeSources,
            cancellationToken,
            progress);

        if (route.Route is "artifact" or "artifactThenPrint")
        {
            progress?.Report(new(
                "status",
                route.Route == "artifactThenPrint"
                    ? "AGY đang chỉnh tài liệu trước khi lập kế hoạch in."
                    : "AGY đang xử lý tài liệu trong workspace riêng."));

            var artifact = await ExecuteArtifactTaskAsync(
                request.SourceIds,
                request.UserRequest.Trim(),
                planner,
                cancellationToken,
                progress);

            if (route.Route == "artifact")
            {
                return new(
                    Job: null,
                    Confidence: 1,
                    Questions: [],
                    Warnings: [],
                    Tier: planner.LastTier,
                    DurationMilliseconds: elapsed.ElapsedMilliseconds,
                    Artifacts: artifact.Sources,
                    Action: "artifact",
                    Message: artifact.Summary);
            }

            var printRequest = string.IsNullOrWhiteSpace(route.PrintRequest)
                ? "In các file kết quả vừa tạo, giữ nguyên bố cục và thu vừa vùng in."
                : route.PrintRequest.Trim();

            return await PlanPrintJobAsync(
                artifact.Sources.Select(source => source.Id).ToArray(),
                printRequest,
                planner,
                cancellationToken,
                progress,
                elapsed,
                artifact.Sources,
                artifact.Summary);
        }

        return await PlanPrintJobAsync(
            request.SourceIds,
            request.UserRequest.Trim(),
            planner,
            cancellationToken,
            progress,
            elapsed);
    }

    private async Task<LocalPlanResult> PlanPrintJobAsync(
        IReadOnlyList<Guid> requestedSourceIds,
        string userRequest,
        LocalWorkflowPlanner planner,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress,
        Stopwatch elapsed,
        IReadOnlyList<UploadedSourceView>? artifacts = null,
        string? artifactMessage = null)
    {
        var effectiveSourceIds = requestedSourceIds.ToList();
        if (effectiveSourceIds.Count == 0)
        {
            var generatedId = await CreateTextSourceAsync(
                userRequest, planner, cancellationToken, progress);
            effectiveSourceIds.Add(generatedId);
        }

        UploadedSource[] selected;
        lock (_sync)
        {
            selected = effectiveSourceIds.Select(id =>
                _sources.TryGetValue(id, out var source)
                    ? source
                    : throw new LocalWorkflowException("Một tệp đã chọn không còn trong phiên.")).ToArray();
        }

        var planningSources = selected.Select(source =>
        {
            var path = Path.GetFullPath(source.Path);
            var metadata = SourceInspector.Inspect(source.Path);
            return new PlanningSource(
                path,
                metadata.Kind.ToString(),
                metadata.PixelWidth,
                metadata.PixelHeight,
                metadata.PageCount ?? 1,
                metadata.Pages?.Select(page => new SourcePageSizeSpec(
                    page.Page, page.WidthMm, page.HeightMm)).ToArray());
        }).ToArray();

        var outcome = await planner.PlanAsync(
            new PlanningRequest(userRequest, planningSources),
            cancellationToken,
            progress);

        if (outcome.Questions.Count > 0)
        {
            return new(
                null,
                outcome.Confidence,
                outcome.Questions,
                outcome.Warnings,
                planner.LastTier,
                elapsed.ElapsedMilliseconds,
                artifacts,
                artifacts is null ? "print" : "artifactThenPrint",
                artifactMessage);
        }

        var plan = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            outcome.Plan,
            planningSources) with
        {
            Policy = new PolicySpec(PreviewPolicy.Required)
        };
        var compiled = PrintPlanCompiler.Compile(plan);
        if (compiled.Batches.Count == 0)
            throw new LocalWorkflowException("Kế hoạch AI không tạo ra trang in nào.");

        var batches = compiled.Batches.Select(compiledBatch =>
        {
            var layout = LayoutEngine.Layout(compiledBatch.Job);
            var pages = layout.Placements.Count == 0
                ? 0
                : layout.Placements.Max(item => item.Page) + 1;
            return new LocalPrintBatch(compiledBatch.Job, layout, pages);
        }).ToArray();

        var itemCount = batches.Sum(batch => batch.Layout.Placements.Count);
        var pageCount = batches.Sum(batch => batch.OutputPageCount);
        if (itemCount is < 1 or > MaxItems || pageCount > MaxOutputPages)
            throw new LocalWorkflowException("Kế hoạch vượt giới hạn 1.000 mục hoặc 20 trang A4.");

        var id = Guid.NewGuid();
        lock (_sync)
        {
            if (_jobs.Count >= 20)
                throw new LocalWorkflowException("Phiên hiện tại tối đa 20 job. Khởi động lại ứng dụng để dọn phiên.");
            _jobs.Add(id, new LocalPrintJob(id, batches, pageCount, effectiveSourceIds.ToArray()));
        }

        var firstLayout = batches[0].Layout;
        return new(
            new LocalJobView(id, pageCount, itemCount, firstLayout.Columns, firstLayout.Rows,
                firstLayout.CapacityPerPage, firstLayout.Rotated),
            outcome.Confidence,
            outcome.Questions,
            outcome.Warnings,
            planner.LastTier,
            elapsed.ElapsedMilliseconds,
            artifacts,
            artifacts is null ? "print" : "artifactThenPrint",
            artifactMessage);
    }
}

public sealed record LocalPlanRequest(IReadOnlyList<Guid> SourceIds, string UserRequest);

public sealed record LocalPlanResult(
    LocalJobView? Job,
    double Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    string? Tier,
    long DurationMilliseconds = 0,
    IReadOnlyList<UploadedSourceView>? Artifacts = null,
    string Action = "print",
    string? Message = null);
