using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using PrintAI.DocumentConversion;
using PrintAI.Domain;
using PrintAI.History;
using PrintAI.Rendering;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    private readonly List<string> _paths = [];
    private readonly List<DesktopPage> _pages = [];
    private readonly string _workDir;
    private readonly JobHistoryStore _history;
    private readonly PendingManualDuplexStore _manualDuplexStore;
    private readonly ManualDuplexCalibrationStore _manualDuplexCalibrationStore;
    private readonly DesktopPlannerSession _planner = new();
    private readonly List<DesktopQueuedRequest> _requestQueue = [];

    private PendingManualDuplexJob? _pendingManualDuplex;

    private int _selectedPage;
    private int _selectedOutputPage;
    private int _outputPageCount;
    private string? _selectedPrinter;
    private string? _status;
    private string? _previewDataUrl;
    private string? _printPath;
    private PrintJobSpec? _activeJob;
    private DesktopPlanResult? _planResult;
    private DesktopGeneralPlanResult? _generalPlanResult;
    private CompiledPrintPlan? _compiledPlan;
    private int _selectedPlanBatch;
    private string? _lastRequest;
    private int _nextRequestOrder = 1;

    public DesktopSession()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintAI");

        _workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(_workDir);
        _history = new JobHistoryStore(Path.Combine(root, "history.json"));
        _manualDuplexStore = new PendingManualDuplexStore(
            Path.Combine(root, "pending-manual-duplex.json"));
        _manualDuplexCalibrationStore = new ManualDuplexCalibrationStore(
            Path.Combine(root, "manual-duplex-profiles.json"));
        _pendingManualDuplex = _manualDuplexStore.Load();

        _planner.ConfigureFromEnvironment();

        var printers = PrinterCapabilityProbe.Enumerate();
        _selectedPrinter =
            printers.FirstOrDefault(p =>
                p.Name.Contains("L3310", StringComparison.OrdinalIgnoreCase))?.Name
            ?? printers.FirstOrDefault(p => p.IsDefault)?.Name
            ?? printers.FirstOrDefault()?.Name;

        if (_pendingManualDuplex is { } pending &&
            printers.Any(p => string.Equals(
                p.Name,
                pending.PrinterName,
                StringComparison.OrdinalIgnoreCase)))
        {
            _selectedPrinter = pending.PrinterName;
            _status =
                pending.Phase == ManualDuplexPendingPhase.WaitingForReinsert
                    ? "Có một job 2 mặt đang chờ back pass. Không in lại mặt trước; hãy nạp lại giấy rồi tiếp tục mặt sau."
                    : "Có một manual-duplex pass ở trạng thái không chắc chắn từ lần chạy trước. App sẽ không tự in lại; hãy kiểm tra giấy trước khi hủy hoặc tiếp tục xử lý.";
        }
    }

    public void PickFiles(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file để in",
            Multiselect = true,
            Filter = "Supported files|*.jpg;*.jpeg;*.png;*.heic;*.heif;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx|All files|*.*"
        };

        if (dialog.ShowDialog(owner) == true)
            AddPaths(dialog.FileNames);
    }

    public void PickFolder(Window owner)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục ảnh/PDF/Office"
        };

        if (dialog.ShowDialog(owner) == true)
            AddPaths([dialog.FolderName]);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var conversionErrors = new List<string>();
        var convertedCount = 0;

        foreach (var inputPath in DesktopSourceCatalog.Expand(paths))
        {
            string path;

            try
            {
                if (OfficeDocumentConverter.IsSupported(inputPath))
                {
                    path = ConvertOfficeSource(inputPath);
                    RegisterOfficeSource(path, inputPath);
                    convertedCount++;
                }
                else
                {
                    path = inputPath;
                }
            }
            catch (Exception ex)
            {
                conversionErrors.Add(
                    $"{Path.GetFileName(inputPath)}: {ex.Message}");
                continue;
            }

            if (_paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                continue;

            _paths.Add(path);
            if (_paths.Count >= 100)
                break;
        }

        RebuildPages();
        _selectedPage = Math.Clamp(
            _selectedPage,
            0,
            Math.Max(0, _pages.Count - 1));

        ResetPlan();
        RebuildPreview();

        if (conversionErrors.Count > 0)
        {
            _status =
                $"Không chuyển được {conversionErrors.Count} file Office. " +
                string.Join(" | ", conversionErrors.Take(3));
        }
        else if (convertedCount > 0 && _previewDataUrl is not null)
        {
            _status =
                $"Đã chuyển {convertedCount} file Office sang PDF để preview/in.";
        }
    }

    public void SelectPage(int index)
    {
        if (index < 0 || index >= _pages.Count)
            return;

        var oldPath = CurrentPage()?.SourcePath;
        _selectedPage = index;
        _selectedOutputPage = 0;

        if (_compiledPlan is null &&
            !string.Equals(
                oldPath,
                CurrentPage()?.SourcePath,
                StringComparison.OrdinalIgnoreCase))
        {
            ResetPlan();
        }

        RebuildPreview();
    }

    public void SelectSource(int index)
    {
        if (index < 0 || index >= _paths.Count)
            return;

        var path = _paths[index];
        var pageIndex = _pages.FindIndex(page =>
            string.Equals(
                page.SourcePath,
                path,
                StringComparison.OrdinalIgnoreCase));

        if (pageIndex >= 0)
            SelectPage(pageIndex);
    }

    public void RemoveSource(int index)
    {
        if (index < 0 || index >= _paths.Count)
            return;

        var currentPath = CurrentPage()?.SourcePath;
        var removedPath = _paths[index];
        _paths.RemoveAt(index);

        RebuildPages();

        if (_pages.Count == 0)
        {
            _selectedPage = 0;
        }
        else if (!string.Equals(
                     currentPath,
                     removedPath,
                     StringComparison.OrdinalIgnoreCase))
        {
            var preservedIndex = _pages.FindIndex(page =>
                string.Equals(
                    page.SourcePath,
                    currentPath,
                    StringComparison.OrdinalIgnoreCase));

            _selectedPage = preservedIndex >= 0
                ? preservedIndex
                : Math.Clamp(_selectedPage, 0, _pages.Count - 1);
        }
        else
        {
            _selectedPage = Math.Clamp(
                _selectedPage,
                0,
                _pages.Count - 1);
        }

        _selectedOutputPage = 0;
        ResetPlan();
        RebuildPreview();
        _status = $"Đã xóa {Path.GetFileName(removedPath)} khỏi nguồn in.";
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
        if (string.IsNullOrWhiteSpace(printerName))
            return;

        _selectedPrinter = printerName;
        var profile = PrinterProfileCatalog.Resolve(printerName);

        _status = profile.IsPhysicallyVerified
            ? $"Profile {profile.Id} đã được hiệu chuẩn vật lý."
            : $"Profile {profile.Id} chưa được hiệu chuẩn vật lý; hãy kiểm tra preview và bản in thử.";
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
        ClearExcelSmartPrintState();
        ResetPlan();
    }

    public void ClearHistory()
    {
        _history.Clear();
        _status = "Đã xóa lịch sử local.";
    }

    public DesktopState BuildState()
    {
        var printers = PrinterCapabilityProbe.Enumerate()
            .Select(p => new DesktopPrinter(
                p.Name,
                p.IsDefault,
                p.SupportsColor,
                p.CanDuplex))
            .ToArray();

        var scanners = GetScanners();
        var page = CurrentPage();
        var job = page is null ? null : CurrentJob(page);
        var planner = BuildPlannerView(job);
        var readiness = BuildReadiness(
            printers.Length,
            scanners.Count,
            planner.Configured);

        return new(
            Files: _paths.Select(DesktopSourceCatalog.InspectSafe).ToArray(),
            RequestQueue: _requestQueue
                .OrderBy(item => item.Order)
                .Select(item => new DesktopQueuedRequestView(
                    item.Id,
                    item.Order,
                    item.Request,
                    item.Mode,
                    item.Status,
                    item.SourcePaths,
                    item.Error))
                .ToArray(),
            Pages: _pages,
            SelectedPage: _selectedPage,
            OutputPageCount: _outputPageCount,
            SelectedOutputPage: _selectedOutputPage,
            Printers: printers,
            SelectedPrinter: _selectedPrinter,
            Scanners: scanners,
            SelectedScanner: _selectedScanner,
            Recipes: GetRecipes(),
            SelectedRecipeId: _selectedRecipeId,
            BuiltInWorkflows: GetBuiltInWorkflows(),
            SelectedWorkflowId: _selectedWorkflowId,
            AutoLayouts: GetAutoLayoutViews(),
            SelectedAutoLayoutId: _selectedAutoLayoutId,
            PreviewDataUrl: _previewDataUrl,
            Status: _status,
            CanPrint: _printPath is not null &&
                      !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintJob: page is not null &&
                         !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintPlan: _compiledPlan is { Batches.Count: > 1 } &&
                          _compiledPlan.Batches.All(batch =>
                              batch.Job.Print.Duplex == DuplexMode.Off) &&
                          !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintAllSources: _pages.Count > 0 &&
                                !string.IsNullOrWhiteSpace(_selectedPrinter),
            Planner: planner,
            ExcelSmartPrint: BuildExcelSmartPrintView(),
            Duplex: BuildDuplexView(job),
            Readiness: readiness,
            History: _history.Read().Take(20).ToArray());
    }

    private string ConvertOfficeSource(string inputPath)
    {
        var normalized = Path.GetFullPath(inputPath);
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..12];

        var outputDirectory = Path.Combine(
            _workDir,
            "converted",
            hash);

        return OfficeDocumentConverter.ConvertToPdf(
            normalized,
            outputDirectory);
    }

    private void RebuildPages()
    {
        _pages.Clear();
        _pages.AddRange(DesktopSourceCatalog.BuildPages(_paths));
    }

    private void RebuildPreview()
    {
        var page = CurrentPage();

        if (page is null)
        {
            _previewDataUrl = null;
            _printPath = null;
            _outputPageCount = 0;
            _status = _paths.Count == 0
                ? null
                : "Không có trang hợp lệ để preview.";
            return;
        }

        try
        {
            var job = CurrentJob(page);
            _outputPageCount = SourceJobRenderer.GetOutputPageCount(job);
            _selectedOutputPage = Math.Clamp(
                _selectedOutputPage,
                0,
                Math.Max(0, _outputPageCount - 1));

            var preview = RequiresMixedRenderer(job)
                ? SourceJobRenderer.RenderMixedA4(
                    job,
                    _selectedOutputPage,
                    dpi: 96)
                : SourceJobRenderer.RenderA4(
                    job,
                    page.SourcePath,
                    page.SourcePageIndex,
                    _selectedOutputPage,
                    dpi: 96);

            var printable = RequiresMixedRenderer(job)
                ? SourceJobRenderer.RenderMixedA4(
                    job,
                    _selectedOutputPage,
                    dpi: 300)
                : SourceJobRenderer.RenderA4(
                    job,
                    page.SourcePath,
                    page.SourcePageIndex,
                    _selectedOutputPage,
                    dpi: 300);

            _previewDataUrl =
                $"data:image/png;base64,{Convert.ToBase64String(preview)}";

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

    private PrintJobSpec CurrentJob(DesktopPage page)
    {
        if (_compiledPlan is not null && _activeJob is not null)
            return _activeJob;

        return _activeJob is not null &&
               _activeJob.Sources.Any(s =>
                   string.Equals(
                       s.Path,
                       page.SourcePath,
                       StringComparison.OrdinalIgnoreCase))
            ? _activeJob
            : CreateDefaultJob(page.SourcePath);
    }

    private void ResetPlan()
    {
        _activeJob = null;
        ClearPlannerResults();
        _lastRequest = null;
        _selectedOutputPage = 0;
        ClearAutoLayouts();
    }

    private void ClearPlannerResults()
    {
        _planResult = null;
        _generalPlanResult = null;
        _compiledPlan = null;
        _selectedPlanBatch = 0;
    }

    private bool IsVerifiedPrinter() =>
        !string.IsNullOrWhiteSpace(_selectedPrinter) &&
        PrinterProfileCatalog.Resolve(_selectedPrinter).IsPhysicallyVerified;

    private static PrintJobSpec CreateDefaultJob(string path) =>
        new(
            Path.GetFileName(path),
            [new SourceSpec(path)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid,
                200,
                287,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            new PrintSettings(),
            new PolicySpec(PreviewPolicy.Required));
}


internal sealed record DesktopQueuedRequest(
    string Id,
    int Order,
    string Request,
    string Mode,
    IReadOnlyList<string> SourcePaths,
    string Status,
    string? Error = null);
