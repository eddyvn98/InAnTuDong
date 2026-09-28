using PrintAI.Domain;
using PrintAI.Planning;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public bool ConfigureAntigravity(string? cliPath = null)
    {
        var configured = _planner.ConfigureAntigravity(cliPath);
        _status = configured
            ? $"Antigravity sẵn sàng: {_planner.Model}."
            : "Không tìm thấy AGY CLI. Cài/đăng nhập Antigravity hoặc đặt PRINTAI_AGY_PATH.";
        return configured;
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

        var tier = string.IsNullOrWhiteSpace(_planner.PlannerTier)
            ? ""
            : $" · AGY {_planner.PlannerTier}";

        _status =
            $"AI PrintPlan{tier}: {result.Decision.Kind} · confidence " +
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

    private static string? FormatPhysicalScaling(
        PhysicalScaleSpec? scaling) =>
        scaling?.Mode switch
        {
            PhysicalScaleMode.MaxFit => "max-fit",
            PhysicalScaleMode.ShrinkOnly => "shrink-only",
            PhysicalScaleMode.Percent => $"{scaling.Percent:0.##}%",
            _ => null
        };

    private static string? FormatPlacement(
        PagePlacementSpec? placement)
    {
        if (placement is null)
            return null;

        var m = placement.Margins;
        var offset =
            Math.Abs(placement.OffsetXMm) > 0.001 ||
            Math.Abs(placement.OffsetYMm) > 0.001
                ? $" · offset {placement.OffsetXMm:0.##},{placement.OffsetYMm:0.##}mm"
                : "";

        return
            $"{placement.Anchor} · lề " +
            $"{m.LeftMm:0.##}/{m.TopMm:0.##}/" +
            $"{m.RightMm:0.##}/{m.BottomMm:0.##}mm" +
            offset;
    }

    private static string? FormatCrop(
        SourceCropSpec? crop) =>
        crop?.Mode switch
        {
            SourceCropMode.AutoTrimWhite => "auto-trim trắng",
            SourceCropMode.CenterToTargetAspect => "crop giữa",
            SourceCropMode.EdgesMm when crop.EdgesMm is { } edges =>
                $"crop {edges.LeftMm:0.##}/" +
                $"{edges.TopMm:0.##}/" +
                $"{edges.RightMm:0.##}/" +
                $"{edges.BottomMm:0.##}mm",
            _ => null
        };

    private static string? FormatBooklet(
        BookletSpec? booklet) =>
        booklet is null
            ? null
            : $"booklet · gutter {booklet.GutterMm:0.##}mm";

    private static string? FormatPoster(
        PosterSpec? poster)
    {
        if (poster is null)
            return null;

        var target =
            poster.TargetWidthMm is double width &&
            poster.TargetHeightMm is double height
                ? $"{width:0.##}×{height:0.##}mm"
                : poster.TargetWidthMm is double onlyWidth
                    ? $"rộng {onlyWidth:0.##}mm"
                    : poster.TargetHeightMm is double onlyHeight
                        ? $"cao {onlyHeight:0.##}mm"
                        : poster.Columns is int columns &&
                          poster.Rows is int rows
                            ? $"{columns}×{rows} tờ"
                            : "auto";

        var extras = new List<string>
        {
            $"overlap {poster.OverlapMm:0.##}mm"
        };

        if (poster.RegistrationMarks)
            extras.Add("dấu căn");

        if (poster.TileLabels)
            extras.Add("số tile");

        return $"poster {target} · {string.Join(" · ", extras)}";
    }

    private static string? FormatVariableItems(
        VariableItemsSpec? spec) =>
        spec is null
            ? null
            : $"mixed-size · {spec.Items.Count} loại · gap {spec.GapMm:0.##}mm";

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
                    PhysicalScaling: FormatPhysicalScaling(
                        batch.Job.Layout.PhysicalScale),
                    Placement: FormatPlacement(
                        batch.Job.Layout.PagePlacement),
                    Crop: FormatCrop(
                        batch.Job.Layout.SourceCrop),
                    Booklet: FormatBooklet(
                        group?.Booklet),
                    Poster: FormatPoster(
                        group?.Poster),
                    VariableItems: FormatVariableItems(
                        group?.VariableItems),
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
