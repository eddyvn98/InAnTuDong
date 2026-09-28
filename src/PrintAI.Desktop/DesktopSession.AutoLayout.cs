using PrintAI.Domain;
using PrintAI.Rendering;
using PrintAI.Workflows;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    private readonly List<AutoLayoutCandidate> _autoLayoutCandidates = [];
    private readonly Dictionary<string, string> _autoLayoutPreviews =
        new(StringComparer.Ordinal);
    private string? _selectedAutoLayoutId;

    public void GenerateAutoLayouts(
        IReadOnlyList<DesktopCompositionItem> items,
        string preference)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
            throw new ArgumentException("Chọn ít nhất một source/page để tự sắp xếp.");

        if (!Enum.TryParse<AutoLayoutPreference>(
                preference,
                ignoreCase: true,
                out var parsedPreference))
        {
            throw new ArgumentException("Preference phải là Balanced, MinCrop hoặc Fill.");
        }

        var selectedPageIndexes = new HashSet<int>();
        var sources = new List<SourceSpec>(items.Count);

        foreach (var item in items)
        {
            if (item.PageIndex < 0 || item.PageIndex >= _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(items), "Source/page đã chọn không còn hợp lệ.");

            if (item.Copies < 1)
                throw new ArgumentOutOfRangeException(nameof(items), "Số bản phải từ 1 trở lên.");

            if (!selectedPageIndexes.Add(item.PageIndex))
                throw new ArgumentException("Mỗi source/page chỉ được xuất hiện một lần.");

            var page = _pages[item.PageIndex];
            sources.Add(new SourceSpec(
                page.SourcePath,
                Copies: item.Copies,
                PageIndex: page.SourcePageIndex));
        }

        _autoLayoutCandidates.Clear();
        _autoLayoutCandidates.AddRange(
            AutoLayoutWorkflow.Generate4x6(
                sources,
                parsedPreference,
                gapMm: 2,
                marginMm: 3,
                maxCandidates: 4));

        if (_autoLayoutCandidates.Count == 0)
            throw new InvalidOperationException("Không tìm được bố cục 4x6 phù hợp.");

        _autoLayoutPreviews.Clear();

        foreach (var candidate in _autoLayoutCandidates)
        {
            var preview = SourceJobRenderer.RenderMixedA4(
                candidate.Job,
                outputPageIndex: 0,
                dpi: 72);

            _autoLayoutPreviews[candidate.Id] =
                $"data:image/png;base64,{Convert.ToBase64String(preview)}";
        }

        _selectedAutoLayoutId = null;
        _status =
            $"Đã tạo {_autoLayoutCandidates.Count} phương án 4x6 cho " +
            $"{sources.Sum(source => source.Copies)} nội dung. Chọn một phương án để áp dụng.";
    }

    public void ApplyAutoLayout(string candidateId)
    {
        var candidate = _autoLayoutCandidates.FirstOrDefault(
            item => string.Equals(item.Id, candidateId, StringComparison.Ordinal))
            ?? throw new ArgumentException("Phương án auto layout không còn hợp lệ.");

        _activeJob = candidate.Job;
        _selectedAutoLayoutId = candidate.Id;
        _selectedWorkflowId = AutoLayoutWorkflow.Id;
        _planResult = null;
        _lastRequest = $"workflow:auto-layout:{candidate.Id}";
        _selectedOutputPage = 0;

        var firstSource = candidate.Job.Sources.First();
        var matchingPage = _pages.FindIndex(page =>
            string.Equals(
                page.SourcePath,
                firstSource.Path,
                StringComparison.OrdinalIgnoreCase) &&
            page.SourcePageIndex == firstSource.PageIndex);

        if (matchingPage >= 0)
            _selectedPage = matchingPage;

        RebuildPreview();

        _status =
            $"Đã áp dụng {candidate.Title} · " +
            $"{candidate.Job.Paper.WidthMm:0.#} x {candidate.Job.Paper.HeightMm:0.#} mm · " +
            $"{candidate.Job.Paper.Orientation}. Kiểm tra preview trước khi in.";
    }

    private IReadOnlyList<DesktopAutoLayoutCandidate> GetAutoLayoutViews() =>
        _autoLayoutCandidates
            .Select(candidate => new DesktopAutoLayoutCandidate(
                candidate.Id,
                candidate.Title,
                candidate.Description,
                $"{candidate.Job.Paper.WidthMm:0.#} x {candidate.Job.Paper.HeightMm:0.#} mm",
                candidate.Job.Paper.Orientation.ToString(),
                candidate.Job.Layout.Fit.ToString(),
                candidate.Score,
                _autoLayoutPreviews.GetValueOrDefault(candidate.Id, "")))
            .ToArray();

    private void ClearAutoLayouts()
    {
        _autoLayoutCandidates.Clear();
        _autoLayoutPreviews.Clear();
        _selectedAutoLayoutId = null;
    }
}
