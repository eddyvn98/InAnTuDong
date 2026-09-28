using PrintAI.Domain;
using PrintAI.Workflows;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void ComposeMixedPages(
        IReadOnlyList<DesktopCompositionItem> items,
        double itemWidthMm,
        double itemHeightMm,
        double gapMm,
        double marginMm,
        bool allowRotate,
        bool cutMarks,
        string fit)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
            throw new ArgumentException("Chọn ít nhất một source/page để bố trí.");

        if (!Enum.TryParse<FitMode>(fit, true, out var fitMode))
            throw new ArgumentException("Fit phải là Contain hoặc Cover.");

        var selectedPageIndexes = new HashSet<int>();
        var sources = new List<SourceSpec>(items.Count);

        foreach (var item in items)
        {
            if (item.PageIndex < 0 || item.PageIndex >= _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(items), "Source/page đã chọn không còn hợp lệ.");

            if (item.Copies is < 1 or > MixedCompositionWorkflow.MaxItemsPerJob)
                throw new ArgumentOutOfRangeException(nameof(items), "Số bản của mỗi source phải từ 1 đến 1000.");

            if (!selectedPageIndexes.Add(item.PageIndex))
                throw new ArgumentException("Mỗi source/page chỉ được xuất hiện một lần trong composition.");

            var page = _pages[item.PageIndex];
            sources.Add(new SourceSpec(
                page.SourcePath,
                Copies: item.Copies,
                PageIndex: page.SourcePageIndex));
        }

        _activeJob = MixedCompositionWorkflow.CreateJob(
            sources,
            new MixedCompositionOptions(
                itemWidthMm,
                itemHeightMm,
                gapMm,
                marginMm,
                allowRotate,
                cutMarks,
                fitMode));

        _selectedPage = items[0].PageIndex;
        _selectedWorkflowId = MixedCompositionWorkflow.Id;
        ClearPlannerResults();
        _lastRequest = "workflow:mixed-composition";
        _selectedOutputPage = 0;

        RebuildPreview();

        _status =
            $"Đã bố trí {sources.Count} source/page · " +
            $"{sources.Sum(source => source.Copies)} nội dung · " +
            $"{itemWidthMm:0.##} x {itemHeightMm:0.##} mm. " +
            $"Output: {_outputPageCount} trang A4.";
    }

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
        ClearPlannerResults();
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
