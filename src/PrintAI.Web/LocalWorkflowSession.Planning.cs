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
            throw new LocalWorkflowException("Yêu cầu in cần có nội dung và tối đa 2.000 ký tự.");
        if (request.SourceIds is null || request.SourceIds.Count > MaxFiles ||
            request.SourceIds.Distinct().Count() != request.SourceIds.Count)
            throw new LocalWorkflowException("Danh sách tệp không hợp lệ.");

        var effectiveSourceIds = request.SourceIds.ToList();
        if (effectiveSourceIds.Count == 0)
        {
            var generatedId = await CreateTextSourceAsync(
                request.UserRequest.Trim(), planner, cancellationToken, progress);
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
            new PlanningRequest(request.UserRequest.Trim(), planningSources),
            cancellationToken,
            progress);

        if (outcome.Questions.Count > 0)
            return new(null, outcome.Confidence, outcome.Questions, outcome.Warnings,
                planner.LastTier, elapsed.ElapsedMilliseconds);

        var plan = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            outcome.Plan,
            planningSources) with
        {
            Policy = new PolicySpec(PreviewPolicy.Required)
        };
        var compiled = PrintPlanCompiler.Compile(plan);
        if (compiled.Batches.Count == 0)
            throw new LocalWorkflowException("Kế hoạch AI không tạo ra trang in nào.");

        if (compiled.Batches.Count > 1)
        {
            throw new LocalWorkflowException(
                $"Kế hoạch gồm {compiled.Batches.Count} nhóm in. Web local hiện đang hoàn thiện preview đa nhóm; hãy tách yêu cầu hoặc dùng một nhóm in trong lúc này.");
        }

        var job = compiled.Batches[0].Job;
        var layout = LayoutEngine.Layout(job);
        var itemCount = layout.Placements.Count;
        var pageCount = itemCount == 0 ? 0 : layout.Placements.Max(item => item.Page) + 1;
        if (itemCount is < 1 or > MaxItems || pageCount > MaxOutputPages)
            throw new LocalWorkflowException("Kế hoạch vượt giới hạn 1.000 mục hoặc 20 trang A4.");

        var id = Guid.NewGuid();
        lock (_sync)
        {
            if (_jobs.Count >= 20)
                throw new LocalWorkflowException("Phiên hiện tại tối đa 20 job. Khởi động lại ứng dụng để dọn phiên.");
            _jobs.Add(id, new LocalPrintJob(id, job, layout, pageCount, effectiveSourceIds.ToArray()));
        }

        return new(
            new LocalJobView(id, pageCount, itemCount, layout.Columns, layout.Rows,
                layout.CapacityPerPage, layout.Rotated),
            outcome.Confidence,
            outcome.Questions,
            outcome.Warnings,
            planner.LastTier,
            elapsed.ElapsedMilliseconds);
    }
}

public sealed record LocalPlanRequest(IReadOnlyList<Guid> SourceIds, string UserRequest);

public sealed record LocalPlanResult(
    LocalJobView? Job,
    double Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    string? Tier,
    long DurationMilliseconds = 0);
