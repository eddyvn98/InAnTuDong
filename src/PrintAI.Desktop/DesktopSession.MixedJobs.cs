using PrintAI.Domain;
using PrintAI.Workflows;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void ComposeCccdFrontBack(
        int frontPageIndex,
        int backPageIndex)
    {
        if (frontPageIndex < 0 || frontPageIndex >= _pages.Count)
            throw new ArgumentOutOfRangeException(nameof(frontPageIndex));

        if (backPageIndex < 0 || backPageIndex >= _pages.Count)
            throw new ArgumentOutOfRangeException(nameof(backPageIndex));

        if (frontPageIndex == backPageIndex)
        {
            throw new ArgumentException(
                "Mặt trước và mặt sau phải là hai source page khác nhau.");
        }

        var front = _pages[frontPageIndex];
        var back = _pages[backPageIndex];

        _activeJob = CccdWorkflow.CreateFrontBackJob(
            new SourceSpec(
                front.SourcePath,
                Copies: 1,
                PageIndex: front.SourcePageIndex),
            new SourceSpec(
                back.SourcePath,
                Copies: 1,
                PageIndex: back.SourcePageIndex));

        _selectedPage = frontPageIndex;
        _selectedWorkflowId = CccdWorkflow.FrontBackId;
        _planResult = null;
        _lastRequest = "workflow:cccd-front-back";
        _selectedOutputPage = 0;

        RebuildPreview();

        _status =
            "Đã ghép CCCD mặt trước + mặt sau trên cùng A4, " +
            "mỗi mặt 85.60 x 53.98 mm. Kiểm tra preview trước khi in.";
    }

    private static bool RequiresMixedRenderer(PrintJobSpec job) =>
        job.Sources.Count > 1 ||
        job.Sources.Any(source => source.PageIndex != 0);
}
