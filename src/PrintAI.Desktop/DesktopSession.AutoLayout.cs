using System.Net.Http;
using PrintAI.Domain;
using PrintAI.Planning;
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

    public async Task GenerateSmartCollagesAsync(
        IReadOnlyList<DesktopCompositionItem> items,
        string? instruction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count != 3 || items.Any(item => item.Copies != 1))
            throw new ArgumentException("Smart Collage hiện cần đúng 3 source/page, mỗi source 1 bản.");

        var sources = new List<SourceSpec>(3);
        var pages = new List<DesktopPage>(3);
        var selectedPageIndexes = new HashSet<int>();

        foreach (var item in items)
        {
            if (item.PageIndex < 0 || item.PageIndex >= _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(items), "Source/page đã chọn không còn hợp lệ.");

            if (!selectedPageIndexes.Add(item.PageIndex))
                throw new ArgumentException("Mỗi source/page chỉ được xuất hiện một lần.");

            var page = _pages[item.PageIndex];
            pages.Add(page);
            sources.Add(new SourceSpec(
                page.SourcePath,
                Copies: 1,
                PageIndex: page.SourcePageIndex));
        }

        var templates = CollageTemplateLibrary.ThreePhoto4x6Portrait();
        _autoLayoutCandidates.Clear();
        _autoLayoutPreviews.Clear();
        _selectedAutoLayoutId = null;

        if (_planner.IsConfigured)
        {
            try
            {
                _status = "AI đang xem 3 ảnh và thiết kế Smart Collage…";

                var plan = await _planner.PlanSmartCollageAsync(
                    pages,
                    templates.Select(template => template.Id).ToArray(),
                    instruction,
                    cancellationToken);

                AddAiCollageCandidates(plan, templates, sources);

                if (_autoLayoutCandidates.Count > 0)
                {
                    var aiCount = _autoLayoutCandidates.Count;
                    AddFallbackCollageCandidates(
                        templates,
                        sources,
                        maxTotal: 4);

                    var fallbackCount = _autoLayoutCandidates.Count - aiCount;
                    _status =
                        fallbackCount == 0
                            ? $"AI đã tạo {aiCount} phương án Smart Collage. " +
                              "Đã phân tích ảnh, chọn template và tự crop/zoom. Chọn một phương án để áp dụng."
                            : $"AI tạo {aiCount} phương án hợp lệ; app bổ sung {fallbackCount} " +
                              "template fallback để đủ lựa chọn. Chọn một phương án để áp dụng.";
                    return;
                }
            }
            catch (Exception ex) when (
                ex is PlannerTransportException or
                PlanningFormatException or
                HttpRequestException or
                InvalidOperationException or
                NotSupportedException)
            {
                AddFallbackCollageCandidates(templates, sources);
                _status =
                    $"AI vision không dùng được ({ex.Message}). " +
                    $"Đã dùng {_autoLayoutCandidates.Count} phương án template fallback.";
                return;
            }
        }

        AddFallbackCollageCandidates(templates, sources);
        _status =
            "Smart Collage vision chưa chuyển sang AGY CLI. " +
            "Đã tạo 4 template fallback local để tiếp tục sử dụng không cần API key.";
    }

    private void AddAiCollageCandidates(
        SmartCollagePlan plan,
        IReadOnlyList<CollageTemplate> templates,
        IReadOnlyList<SourceSpec> sources)
    {
        var templateMap = templates.ToDictionary(
            template => template.Id,
            StringComparer.Ordinal);

        foreach (var proposal in plan.Candidates.Take(4))
        {
            if (!templateMap.TryGetValue(proposal.TemplateId, out var template))
                continue;

            var assignments = proposal.Frames
                .Select(frame => new CollageFrameAssignment(
                    frame.FrameIndex,
                    frame.SourceIndex,
                    frame.Scale,
                    frame.OffsetX,
                    frame.OffsetY))
                .ToArray();

            var job = CollageTemplateLibrary.CreateJob(
                template,
                sources,
                assignments);

            var validation = PrintJobValidator.Validate(job);
            if (!validation.IsValid)
                continue;

            var candidate = new AutoLayoutCandidate(
                Id: $"ai-{_autoLayoutCandidates.Count}-{template.Id}",
                Title: template.Title,
                Description:
                    $"AI {proposal.Confidence:P0} · {proposal.Reason}",
                Columns: 0,
                Rows: 0,
                Score: proposal.Confidence,
                Job: job);

            AddRenderedCandidate(candidate);
        }
    }

    private void AddFallbackCollageCandidates(
        IReadOnlyList<CollageTemplate> templates,
        IReadOnlyList<SourceSpec> sources,
        int maxTotal = 4)
    {
        var preferredIds = new[]
        {
            "hero-left-two-right",
            "hero-top-two-bottom",
            "three-rounded-columns",
            "center-circle-two-sides"
        };

        foreach (var id in preferredIds)
        {
            if (_autoLayoutCandidates.Count >= maxTotal)
                break;

            if (_autoLayoutCandidates.Any(candidate =>
                    candidate.Id.EndsWith(id, StringComparison.Ordinal)))
            {
                continue;
            }

            var template = templates.First(item => item.Id == id);
            var job = CollageTemplateLibrary.CreateJob(template, sources);

            AddRenderedCandidate(new AutoLayoutCandidate(
                Id: $"fallback-{template.Id}",
                Title: template.Title,
                Description: $"Template fallback · {string.Join(" · ", template.Tags)}",
                Columns: 0,
                Rows: 0,
                Score: 0,
                Job: job));
        }
    }

    private void AddRenderedCandidate(AutoLayoutCandidate candidate)
    {
        _autoLayoutCandidates.Add(candidate);

        var preview = SourceJobRenderer.RenderMixedA4(
            candidate.Job,
            outputPageIndex: 0,
            dpi: 72);

        _autoLayoutPreviews[candidate.Id] =
            $"data:image/png;base64,{Convert.ToBase64String(preview)}";
    }

    public void ApplyAutoLayout(string candidateId)
    {
        var candidate = _autoLayoutCandidates.FirstOrDefault(
            item => string.Equals(item.Id, candidateId, StringComparison.Ordinal))
            ?? throw new ArgumentException("Phương án auto layout không còn hợp lệ.");

        _activeJob = candidate.Job;
        _selectedAutoLayoutId = candidate.Id;
        _selectedWorkflowId = AutoLayoutWorkflow.Id;
        ClearPlannerResults();
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
