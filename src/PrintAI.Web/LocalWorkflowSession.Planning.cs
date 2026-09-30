using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Planning;
using PrintAI.SourceInspection;

namespace PrintAI.Web;

public sealed partial class LocalWorkflowSession
{
    public async Task<LocalPlanResult> PlanJobAsync(
        LocalPlanRequest request,
        LocalWorkflowPlanner planner,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserRequest) || request.UserRequest.Length > 2000)
            throw new LocalWorkflowException("Yêu cầu in cần có nội dung và tối đa 2.000 ký tự.");
        if (request.SourceIds is null || request.SourceIds.Count is < 1 or > MaxFiles ||
            request.SourceIds.Distinct().Count() != request.SourceIds.Count)
            throw new LocalWorkflowException("Chọn ít nhất một tệp đã tải lên.");

        UploadedSource[] selected;
        lock (_sync)
        {
            selected = request.SourceIds.Select(id =>
                _sources.TryGetValue(id, out var source)
                    ? source
                    : throw new LocalWorkflowException("Một tệp đã chọn không còn trong phiên.")).ToArray();
        }

        var paths = selected.Select(source => Path.GetFullPath(source.Path)).ToArray();
        var planningSources = selected.Select((source, index) =>
        {
            var metadata = SourceInspector.Inspect(source.Path);
            return new PlanningSource(
                paths[index],
                metadata.Kind.ToString(),
                metadata.PixelWidth,
                metadata.PixelHeight,
                metadata.PageCount ?? 1,
                metadata.Pages?.Select(page => new SourcePageSizeSpec(
                    page.Page, page.WidthMm, page.HeightMm)).ToArray());
        }).ToArray();
        var outcome = await planner.PlanAsync(
            new PlanningRequest(request.UserRequest.Trim(), planningSources), cancellationToken);
        if (outcome.Questions.Count > 0)
            return new(null, outcome.Confidence, outcome.Questions, outcome.Warnings, planner.LastTier);

        var job = PlannerSourceBinder.BindToAllowedSources(outcome.Job, paths) with
        {
            Policy = new PolicySpec(PreviewPolicy.Required)
        };
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
            _jobs.Add(id, new LocalPrintJob(id, job, layout, pageCount));
        }

        return new(
            new LocalJobView(id, pageCount, itemCount, layout.Columns, layout.Rows,
                layout.CapacityPerPage, layout.Rotated),
            outcome.Confidence,
            outcome.Questions,
            outcome.Warnings,
            planner.LastTier);
    }
}

public sealed record LocalPlanRequest(IReadOnlyList<Guid> SourceIds, string UserRequest);

public sealed record LocalPlanResult(
    LocalJobView? Job,
    double Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    string? Tier);
