using PrintAI.Domain;
using PrintAI.Planning;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void ConfigurePlanner(
        string endpoint,
        string model,
        string? apiKey)
    {
        _planner.Configure(endpoint, model, apiKey);
        _status =
            $"AI đã cấu hình: {model}. API key chỉ giữ trong phiên chạy hiện tại.";
    }

    public async Task PlanAsync(string request, string mode)
    {
        _ = CurrentPage()
            ?? throw new InvalidOperationException(
                "Chọn ít nhất một file/trang trước khi dùng AI.");

        if (!Enum.TryParse<SafetyMode>(
                mode,
                ignoreCase: true,
                out var safetyMode))
        {
            throw new ArgumentException(
                "Safety mode must be Safe, Smart or Auto.");
        }

        _status = "AI đang lập PrintPlan 2.0…";
        _lastRequest = request;

        var result = await _planner.PlanGeneralAsync(
            request,
            _paths,
            safetyMode,
            IsVerifiedPrinter());

        _generalPlanResult = result;
        _planResult = null;
        _compiledPlan = result.Compiled;
        _selectedPlanBatch = 0;

        ActivatePlanBatch(0, rebuildPreview: true);

        var questions = result.Outcome.Questions.Count == 0
            ? ""
            : $" Cần trả lời: {string.Join(" | ", result.Outcome.Questions)}";

        var duplexGuard =
            result.Compiled.Batches.Count > 1 &&
            result.Compiled.Batches.Any(batch =>
                batch.Job.Print.Duplex != DuplexMode.Off);

        _status =
            $"AI PrintPlan: {result.Decision.Kind} · confidence " +
            $"{result.Outcome.Confidence:P0} · " +
            $"{result.Compiled.Batches.Count} batch. " +
            result.Decision.Reason +
            (duplexGuard
                ? " Plan có duplex: kiểm tra/in từng batch để giữ đúng thứ tự giấy."
                : "") +
            questions;

        _history.Append(new(
            DateTimeOffset.Now,
            Action: "plan-v2",
            Status: result.Decision.Kind.ToString(),
            Request: request,
            Printer: _selectedPrinter,
            JobName: result.Outcome.Plan.PlanName,
            Detail:
                $"{result.Compiled.Batches.Count} batch · " +
                result.Decision.Reason));

        if (result.Decision.Kind == PolicyDecisionKind.Direct)
        {
            if (result.Compiled.Batches.Count == 1)
                PrintJob();
            else if (!duplexGuard)
                PrintPlan();
        }
    }

    public void SelectPlanBatch(int index)
    {
        ActivatePlanBatch(index, rebuildPreview: true);

        if (_compiledPlan is { } plan)
        {
            _status =
                $"Đang xem batch {index + 1}/{plan.Batches.Count}: " +
                plan.Batches[index].Job.JobName;
        }
    }

    private void ActivatePlanBatch(
        int index,
        bool rebuildPreview)
    {
        if (_compiledPlan is null)
            throw new InvalidOperationException("Không có PrintPlan đang hoạt động.");

        if (index < 0 || index >= _compiledPlan.Batches.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _selectedPlanBatch = index;
        _activeJob = _compiledPlan.Batches[index].Job;
        _selectedOutputPage = 0;

        var firstSource = _activeJob.Sources.FirstOrDefault();
        if (firstSource is not null)
        {
            var matchingPage = _pages.FindIndex(page =>
                string.Equals(
                    page.SourcePath,
                    firstSource.Path,
                    StringComparison.OrdinalIgnoreCase) &&
                page.SourcePageIndex == firstSource.PageIndex);

            if (matchingPage >= 0)
                _selectedPage = matchingPage;
        }

        if (rebuildPreview)
            RebuildPreview();
    }

    public void ApplyJobEdits(DesktopJobEdits edits)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException(
                "Không có trang đang chọn.");

        var current = CurrentJob(page);
        _activeJob = DesktopJobEditor.Apply(current, edits);
        ClearPlannerResults();
        _selectedOutputPage = 0;

        RebuildPreview();
        _status =
            "Đã áp dụng chỉnh sửa deterministic và render lại preview.";
    }

    private DesktopPlannerView BuildPlannerView(PrintJobSpec? job)
    {
        var batches = _compiledPlan?.Batches
            .Select((batch, index) =>
            {
                var group = _generalPlanResult?
                    .Outcome.Plan.OutputGroups[batch.GroupIndex];

                return new DesktopPrintBatchView(
                    Index: index,
                    Name: batch.Job.JobName,
                    SetNumber: batch.SetNumber,
                    SourcePageCount: batch.Job.Sources.Count,
                    ColorMode: batch.Job.Print.ColorMode.ToString(),
                    Duplex: batch.Job.Print.Duplex.ToString(),
                    Paper:
                        $"{batch.Job.Paper.WidthMm:0.#} × " +
                        $"{batch.Job.Paper.HeightMm:0.#} mm",
                    PagesPerSheet: group?.NUp?.PagesPerSheet,
                    ItemBorder: batch.Job.Layout.ItemBorder,
                    CanAutoSequence:
                        batch.Job.Print.Duplex == DuplexMode.Off);
            })
            .ToArray() ?? [];

        return new(
            Configured: _planner.IsConfigured,
            Endpoint: _planner.Endpoint,
            Model: _planner.Model,
            Request: _lastRequest,
            Decision:
                _generalPlanResult?.Decision.Kind.ToString() ??
                _planResult?.Decision.Kind.ToString(),
            Confidence:
                _generalPlanResult?.Outcome.Confidence ??
                _planResult?.Outcome.Confidence,
            Questions:
                _generalPlanResult?.Outcome.Questions ??
                _planResult?.Outcome.Questions ??
                [],
            Warnings:
                _generalPlanResult?.Outcome.Warnings ??
                _planResult?.Outcome.Warnings ??
                [],
            IsGeneralPlan: _compiledPlan is not null,
            SelectedBatch: _selectedPlanBatch,
            Batches: batches,
            Job: job is null ? null : DesktopJobView.From(job));
    }
}
