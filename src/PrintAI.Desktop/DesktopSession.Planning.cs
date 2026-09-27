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
        var page = CurrentPage()
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

        _status = "AI đang lập PrintJobSpec…";
        _lastRequest = request;

        var result = await _planner.PlanAsync(
            request,
            page,
            safetyMode,
            IsVerifiedPrinter());

        _planResult = result;
        _activeJob = result.Outcome.Job;
        _selectedOutputPage = 0;
        RebuildPreview();

        var questions = result.Outcome.Questions.Count == 0
            ? ""
            : $" Cần trả lời: {string.Join(" | ", result.Outcome.Questions)}";

        _status =
            $"AI: {result.Decision.Kind} · confidence " +
            $"{result.Outcome.Confidence:P0}. " +
            result.Decision.Reason +
            questions;

        _history.Append(new(
            DateTimeOffset.Now,
            Action: "plan",
            Status: result.Decision.Kind.ToString(),
            Request: request,
            Printer: _selectedPrinter,
            JobName: result.Outcome.Job.JobName,
            Detail: result.Decision.Reason));

        if (result.Decision.Kind == PolicyDecisionKind.Direct)
            PrintJob();
    }

    public void ApplyJobEdits(DesktopJobEdits edits)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException(
                "Không có trang đang chọn.");

        var current = CurrentJob(page);
        _activeJob = DesktopJobEditor.Apply(current, edits);
        _planResult = null;
        _selectedOutputPage = 0;

        RebuildPreview();
        _status =
            "Đã áp dụng chỉnh sửa deterministic và render lại preview.";
    }

    private DesktopPlannerView BuildPlannerView(PrintJobSpec? job) =>
        new(
            Configured: _planner.IsConfigured,
            Endpoint: _planner.Endpoint,
            Model: _planner.Model,
            Request: _lastRequest,
            Decision: _planResult?.Decision.Kind.ToString(),
            Confidence: _planResult?.Outcome.Confidence,
            Questions: _planResult?.Outcome.Questions ?? [],
            Warnings: _planResult?.Outcome.Warnings ?? [],
            Job: job is null ? null : DesktopJobView.From(job));
}
