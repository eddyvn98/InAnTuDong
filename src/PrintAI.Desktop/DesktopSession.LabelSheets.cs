using PrintAI.Domain;
using PrintAI.Workflows;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void ApplyCustomLabelSheet(
        double itemWidthMm,
        double itemHeightMm,
        int copies,
        double gapMm,
        double marginMm,
        bool allowRotate,
        bool cutMarks,
        string fit)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException(
                "Chọn source page trước khi tạo label sheet.");

        if (!Enum.TryParse<FitMode>(fit, true, out var fitMode))
            throw new ArgumentException("Fit phải là Contain hoặc Cover.");

        _activeJob = LabelSheetWorkflow.CreateJob(
            new SourceSpec(
                page.SourcePath,
                Copies: 1,
                PageIndex: page.SourcePageIndex),
            new LabelSheetOptions(
                itemWidthMm,
                itemHeightMm,
                copies,
                gapMm,
                marginMm,
                allowRotate,
                cutMarks,
                fitMode));

        _selectedWorkflowId = LabelSheetWorkflow.CustomId;
        ClearPlannerResults();
        _lastRequest = "workflow:label-custom-sheet";
        _selectedOutputPage = 0;

        RebuildPreview();

        _status =
            $"Label sheet {itemWidthMm:0.##} x {itemHeightMm:0.##} mm · " +
            $"{copies} bản · {gapMm:0.##} mm gap. " +
            $"Output: {_outputPageCount} trang A4.";
    }
}
