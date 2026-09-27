using System.IO;
using System.Windows;
using Microsoft.Win32;
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
            printers.FirstOrDefault(p =>
                p.Name.Contains("L3310", StringComparison.OrdinalIgnoreCase))?.Name
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
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục ảnh/PDF"
        };

        if (dialog.ShowDialog(owner) == true)
            AddPaths([dialog.FolderName]);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in DesktopSourceCatalog.Expand(paths))
        {
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
    }

    public void SelectPage(int index)
    {
        if (index < 0 || index >= _pages.Count)
            return;

        var oldPath = CurrentPage()?.SourcePath;
        _selectedPage = index;
        _selectedOutputPage = 0;

        if (!string.Equals(
                oldPath,
                CurrentPage()?.SourcePath,
                StringComparison.OrdinalIgnoreCase))
        {
            ResetPlan();
        }

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

    public DesktopState BuildState()
    {
        var printers = PrinterCapabilityProbe.Enumerate()
            .Select(p => new DesktopPrinter(
                p.Name,
                p.IsDefault,
                p.SupportsColor,
                p.CanDuplex))
            .ToArray();

        var page = CurrentPage();
        var job = page is null ? null : CurrentJob(page);

        return new(
            Files: _paths.Select(DesktopSourceCatalog.InspectSafe).ToArray(),
            Pages: _pages,
            SelectedPage: _selectedPage,
            OutputPageCount: _outputPageCount,
            SelectedOutputPage: _selectedOutputPage,
            Printers: printers,
            SelectedPrinter: _selectedPrinter,
            PreviewDataUrl: _previewDataUrl,
            Status: _status,
            CanPrint: _printPath is not null &&
                      !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintJob: page is not null &&
                         !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintAllSources: _pages.Count > 0 &&
                                !string.IsNullOrWhiteSpace(_selectedPrinter),
            Planner: BuildPlannerView(job),
            History: _history.Read().Take(20).ToArray());
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

            var preview = SourceJobRenderer.RenderA4(
                job,
                page.SourcePath,
                page.SourcePageIndex,
                _selectedOutputPage,
                dpi: 96);

            var printable = SourceJobRenderer.RenderA4(
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

    private PrintJobSpec CurrentJob(DesktopPage page) =>
        _activeJob is not null &&
        _activeJob.Sources.Any(s =>
            string.Equals(
                s.Path,
                page.SourcePath,
                StringComparison.OrdinalIgnoreCase))
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
        _selectedPrinter?.Contains(
            "L3310",
            StringComparison.OrdinalIgnoreCase) == true;

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
