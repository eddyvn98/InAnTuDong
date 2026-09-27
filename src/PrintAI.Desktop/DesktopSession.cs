using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrintAI.Domain;
using PrintAI.History;
using PrintAI.Planning;
using PrintAI.Rendering;
using PrintAI.SourceInspection;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

public sealed class DesktopSession
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };

    private readonly List<string> _paths = [];
    private readonly List<DesktopPage> _pages = [];
    private readonly string _workDir;
    private readonly JobHistoryStore _history;
    private readonly DesktopPlannerSession _planner = new();

    private int _selectedPage;
    private int _selectedOutputPage;
    private int _outputPageCount;
    private string? _selectedPrinter;
    private string? _status;
    private string? _previewDataUrl;
    private string? _printPath;
    private PrintJobSpec? _activeJob;
    private DesktopPlanResult? _planResult;
    private string? _lastRequest;

    public DesktopSession()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintAI");

        _workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(_workDir);
        _history = new JobHistoryStore(Path.Combine(root, "history.json"));

        _planner.ConfigureFromEnvironment();

        var printers = PrinterCapabilityProbe.Enumerate();
        _selectedPrinter =
            printers.FirstOrDefault(p => p.Name.Contains("L3310", StringComparison.OrdinalIgnoreCase))?.Name
            ?? printers.FirstOrDefault(p => p.IsDefault)?.Name
            ?? printers.FirstOrDefault()?.Name;
    }

    public void PickFiles(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file để in",
            Multiselect = true,
            Filter = "Supported files|*.jpg;*.jpeg;*.png;*.pdf|All files|*.*"
        };

        if (dialog.ShowDialog(owner) == true)
            AddPaths(dialog.FileNames);
    }

    public void PickFolder(Window owner)
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục ảnh/PDF" };
        if (dialog.ShowDialog(owner) == true)
            AddPaths([dialog.FolderName]);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in Expand(paths))
        {
            if (_paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                continue;

            _paths.Add(path);
            if (_paths.Count >= 100)
                break;
        }

        RebuildPages();
        _selectedPage = Math.Clamp(_selectedPage, 0, Math.Max(0, _pages.Count - 1));
        ResetPlan();
        RebuildPreview();
    }

    public void SelectPage(int index)
    {
        if (index < 0 || index >= _pages.Count)
            return;

        var oldPath = CurrentPage()?.SourcePath;
        _selectedPage = index;
        _selectedOutputPage = 0;

        if (!string.Equals(oldPath, CurrentPage()?.SourcePath, StringComparison.OrdinalIgnoreCase))
            ResetPlan();

        RebuildPreview();
    }

    public void SelectOutputPage(int index)
    {
        if (index < 0 || index >= _outputPageCount)
            return;

        _selectedOutputPage = index;
        RebuildPreview();
    }

    public void SelectPrinter(string? printerName)
    {
        if (!string.IsNullOrWhiteSpace(printerName))
            _selectedPrinter = printerName;
    }

    public void ConfigurePlanner(string endpoint, string model, string? apiKey)
    {
        _planner.Configure(endpoint, model, apiKey);
        _status = $"AI đã cấu hình: {model}. API key chỉ giữ trong phiên chạy hiện tại.";
    }

    public async Task PlanAsync(string request, string mode)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException("Chọn ít nhất một file/trang trước khi dùng AI.");

        if (!Enum.TryParse<SafetyMode>(mode, ignoreCase: true, out var safetyMode))
            throw new ArgumentException("Safety mode must be Safe, Smart or Auto.");

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
            $"AI: {result.Decision.Kind} · confidence {result.Outcome.Confidence:P0}. " +
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
            ?? throw new InvalidOperationException("Không có trang đang chọn.");

        var current = CurrentJob(page);
        _activeJob = DesktopJobEditor.Apply(current, edits);
        _planResult = null;
        _selectedOutputPage = 0;
        RebuildPreview();
        _status = "Đã áp dụng chỉnh sửa deterministic và render lại preview.";
    }

    public void Clear()
    {
        _paths.Clear();
        _pages.Clear();
        _selectedPage = 0;
        _selectedOutputPage = 0;
        _outputPageCount = 0;
        _previewDataUrl = null;
        _printPath = null;
        _status = null;
        ResetPlan();
    }

    public void ClearHistory()
    {
        _history.Clear();
        _status = "Đã xóa lịch sử local.";
    }

    public void PrintCurrent()
    {
        if (_printPath is null)
        {
            _status = "Chưa có trang preview có thể in.";
            return;
        }

        var result = Submit(_printPath);
        _status = Describe(result);
        RecordPrint("print-page", result, CurrentJob(CurrentPage()!));
    }

    public void PrintJob()
    {
        var page = CurrentPage();
        if (page is null)
        {
            _status = "Không có trang để in.";
            return;
        }

        var job = CurrentJob(page);
        var pageCount = SourceJobRenderer.GetOutputPageCount(job);
        var submitted = 0;

        for (var outputPage = 0; outputPage < pageCount; outputPage++)
        {
            try
            {
                var png = SourceJobRenderer.RenderA4(
                    job, page.SourcePath, page.SourcePageIndex, outputPage, dpi: 300);
                var path = Path.Combine(_workDir, $"job-{outputPage:D4}.png");
                File.WriteAllBytes(path, png);

                var result = Submit(path);
                if (result.State == PrintSubmissionState.Failed)
                {
                    _status = $"Dừng ở output {outputPage + 1}/{pageCount}: {result.Error}";
                    RecordPrint("print-job", result, job);
                    return;
                }

                submitted++;
            }
            catch (Exception ex)
            {
                _status = $"Dừng ở output {outputPage + 1}/{pageCount}: {ex.Message}";
                _history.Append(new(
                    DateTimeOffset.Now, "print-job", "Failed",
                    _lastRequest, _selectedPrinter, job.JobName, ex.Message));
                return;
            }
        }

        _status = $"Đã gửi {submitted}/{pageCount} output page tới spooler.";
        _history.Append(new(
            DateTimeOffset.Now, "print-job", "Submitted",
            _lastRequest, _selectedPrinter, job.JobName, _status));
    }

    public void PrintAllSources()
    {
        var submitted = 0;

        foreach (var page in _pages)
        {
            try
            {
                var job = CreateDefaultJob(page.SourcePath);
                var png = SourceJobRenderer.RenderA4(
                    job, page.SourcePath, page.SourcePageIndex, 0, dpi: 300);
                var path = Path.Combine(_workDir, $"source-{submitted:D4}.png");
                File.WriteAllBytes(path, png);

                var result = Submit(path);
                if (result.State == PrintSubmissionState.Failed)
                {
                    _status = $"Dừng ở source page {submitted + 1}/{_pages.Count}: {result.Error}";
                    return;
                }

                submitted++;
            }
            catch (Exception ex)
            {
                _status = $"Dừng ở source page {submitted + 1}/{_pages.Count}: {ex.Message}";
                return;
            }
        }

        _status = $"Đã gửi {submitted}/{_pages.Count} source page tới spooler.";
    }

    public DesktopState BuildState()
    {
        var printers = PrinterCapabilityProbe.Enumerate()
            .Select(p => new DesktopPrinter(p.Name, p.IsDefault, p.SupportsColor, p.CanDuplex))
            .ToArray();

        var page = CurrentPage();
        var job = page is null ? null : CurrentJob(page);

        return new(
            Files: _paths.Select(InspectSafe).ToArray(),
            Pages: _pages,
            SelectedPage: _selectedPage,
            OutputPageCount: _outputPageCount,
            SelectedOutputPage: _selectedOutputPage,
            Printers: printers,
            SelectedPrinter: _selectedPrinter,
            PreviewDataUrl: _previewDataUrl,
            Status: _status,
            CanPrint: _printPath is not null && !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintJob: page is not null && !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintAllSources: _pages.Count > 0 && !string.IsNullOrWhiteSpace(_selectedPrinter),
            Planner: BuildPlannerView(job),
            History: _history.Read().Take(20).ToArray());
    }

    private void RebuildPages()
    {
        _pages.Clear();

        foreach (var path in _paths)
        {
            try
            {
                var metadata = SourceInspector.Inspect(path);
                var count = metadata.Kind == SourceKind.Pdf ? metadata.PageCount ?? 0 : 1;

                for (var sourcePage = 0; sourcePage < count; sourcePage++)
                {
                    _pages.Add(new(
                        _pages.Count,
                        path,
                        Path.GetFileName(path),
                        sourcePage,
                        metadata.Kind == SourceKind.Pdf
                            ? $"Trang {sourcePage + 1}/{count}"
                            : "Ảnh"));
                }
            }
            catch
            {
            }
        }
    }

    private void RebuildPreview()
    {
        var page = CurrentPage();
        if (page is null)
        {
            _previewDataUrl = null;
            _printPath = null;
            _outputPageCount = 0;
            _status = _paths.Count == 0 ? null : "Không có trang hợp lệ để preview.";
            return;
        }

        try
        {
            var job = CurrentJob(page);
            _outputPageCount = SourceJobRenderer.GetOutputPageCount(job);
            _selectedOutputPage = Math.Clamp(
                _selectedOutputPage, 0, Math.Max(0, _outputPageCount - 1));

            var preview = SourceJobRenderer.RenderA4(
                job, page.SourcePath, page.SourcePageIndex, _selectedOutputPage, dpi: 96);
            var printable = SourceJobRenderer.RenderA4(
                job, page.SourcePath, page.SourcePageIndex, _selectedOutputPage, dpi: 300);

            _previewDataUrl = $"data:image/png;base64,{Convert.ToBase64String(preview)}";
            _printPath = Path.Combine(_workDir, "current-print.png");
            File.WriteAllBytes(_printPath, printable);
        }
        catch (Exception ex)
        {
            _previewDataUrl = null;
            _printPath = null;
            _outputPageCount = 0;
            _status = $"Không tạo được preview: {ex.Message}";
        }
    }

    private DesktopPage? CurrentPage() =>
        _selectedPage >= 0 && _selectedPage < _pages.Count
            ? _pages[_selectedPage]
            : null;

    private PrintJobSpec CurrentJob(DesktopPage page) =>
        _activeJob is not null &&
        _activeJob.Sources.Any(s =>
            string.Equals(s.Path, page.SourcePath, StringComparison.OrdinalIgnoreCase))
            ? _activeJob
            : CreateDefaultJob(page.SourcePath);

    private void ResetPlan()
    {
        _activeJob = null;
        _planResult = null;
        _lastRequest = null;
        _selectedOutputPage = 0;
    }

    private bool IsVerifiedPrinter() =>
        _selectedPrinter?.Contains("L3310", StringComparison.OrdinalIgnoreCase) == true;

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

    private PrintSubmissionResult Submit(string path)
    {
        if (string.IsNullOrWhiteSpace(_selectedPrinter))
            return new(PrintSubmissionState.Failed, "", "", Error: "Không tìm thấy máy in.");

        var profile = IsVerifiedPrinter()
            ? PrinterDeviceProfile.EpsonL3310Calibrated
            : new PrinterDeviceProfile("default", _selectedPrinter);

        return WindowsSpoolerPrinter.SubmitA4Png(_selectedPrinter, path, profile);
    }

    private void RecordPrint(string action, PrintSubmissionResult result, PrintJobSpec job) =>
        _history.Append(new(
            DateTimeOffset.Now,
            action,
            result.State.ToString(),
            _lastRequest,
            _selectedPrinter,
            job.JobName,
            result.Error ?? result.JobId?.ToString()));

    private static string Describe(PrintSubmissionResult result) =>
        result.State == PrintSubmissionState.Failed
            ? $"In lỗi: {result.Error}"
            : result.JobId is int id
                ? $"Đã gửi tới spooler · Job #{id}"
                : "Đã gửi tới spooler.";

    private static PrintJobSpec CreateDefaultJob(string path) =>
        new(
            Path.GetFileName(path),
            [new SourceSpec(path)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid, 200, 287,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            new PrintSettings(),
            new PolicySpec(PreviewPolicy.Required));

    private static DesktopFile InspectSafe(string path)
    {
        try
        {
            var metadata = SourceInspector.Inspect(path);
            return new(
                Path.GetFileName(path), path, metadata.Kind.ToString(),
                metadata.PixelWidth, metadata.PixelHeight, metadata.PageCount, null);
        }
        catch (Exception ex)
        {
            return new(Path.GetFileName(path), path, "Error", null, null, null, ex.Message);
        }
    }

    private static IEnumerable<string> Expand(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && Supported(path))
            {
                yield return Path.GetFullPath(path);
                continue;
            }

            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
                if (Supported(file))
                    yield return Path.GetFullPath(file);
        }
    }

    private static bool Supported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));
}

public sealed record DesktopState(
    IReadOnlyList<DesktopFile> Files,
    IReadOnlyList<DesktopPage> Pages,
    int SelectedPage,
    int OutputPageCount,
    int SelectedOutputPage,
    IReadOnlyList<DesktopPrinter> Printers,
    string? SelectedPrinter,
    string? PreviewDataUrl,
    string? Status,
    bool CanPrint,
    bool CanPrintJob,
    bool CanPrintAllSources,
    DesktopPlannerView Planner,
    IReadOnlyList<JobHistoryEntry> History);

public sealed record DesktopPlannerView(
    bool Configured,
    string? Endpoint,
    string? Model,
    string? Request,
    string? Decision,
    double? Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    DesktopJobView? Job);

public sealed record DesktopJobView(
    string Mode,
    double ItemWidthMm,
    double ItemHeightMm,
    double GapMm,
    double MarginMm,
    int Copies,
    bool AllowRotate,
    bool CutMarks,
    string Fit)
{
    public static DesktopJobView From(PrintJobSpec job) =>
        new(
            job.Layout.Mode.ToString(),
            job.Layout.ItemWidthMm,
            job.Layout.ItemHeightMm,
            job.Layout.GapMm,
            job.Layout.MarginMm,
            job.Sources.FirstOrDefault()?.Copies ?? 1,
            job.Layout.AllowRotate,
            job.Layout.CutMarks,
            job.Layout.Fit.ToString());
}

public sealed record DesktopPage(
    int GlobalIndex,
    string SourcePath,
    string SourceName,
    int SourcePageIndex,
    string PageLabel);

public sealed record DesktopFile(
    string Name,
    string Path,
    string Kind,
    int? PixelWidth,
    int? PixelHeight,
    int? PageCount,
    string? Error);

public sealed record DesktopPrinter(
    string Name,
    bool IsDefault,
    bool SupportsColor,
    bool CanDuplex);
