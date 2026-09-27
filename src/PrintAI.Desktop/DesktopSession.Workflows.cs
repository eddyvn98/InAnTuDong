using PrintAI.Workflows;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    private string? _selectedWorkflowId;

    public void ApplyBuiltInWorkflow(string workflowId)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException(
                "Chọn source page trước khi áp dụng workflow.");

        var preset = BuiltInWorkflowCatalog.Get(workflowId);

        _selectedWorkflowId = preset.Id;
        _activeJob = preset.CreateJob(page.SourcePath);
        _planResult = null;
        _lastRequest = $"workflow:{preset.Id}";
        _selectedOutputPage = 0;

        RebuildPreview();

        _status =
            $"Đã áp dụng {preset.Name}: " +
            $"{preset.Layout.ItemWidthMm:0.##} x " +
            $"{preset.Layout.ItemHeightMm:0.##} mm · " +
            $"{preset.SourceCopies} bản. Kiểm tra preview trước khi in.";
    }

    private IReadOnlyList<DesktopWorkflowPreset> GetBuiltInWorkflows() =>
        BuiltInWorkflowCatalog.All
            .Select(x => new DesktopWorkflowPreset(
                x.Id,
                x.Name,
                x.Category.ToString(),
                x.Description,
                x.Layout.ItemWidthMm,
                x.Layout.ItemHeightMm,
                x.SourceCopies))
            .ToArray();
}
