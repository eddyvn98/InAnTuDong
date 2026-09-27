using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrintAI.Domain;
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
    private readonly DesktopPlanningController _planning = new();
    private readonly string _workDir;
    private int _selectedPage;
    private string? _selectedPrinter;
    private string? _status;
    private string? _previewDataUrl;
    private string? _printPath;

    public DesktopSession()
    {
        _workDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintAI", "work");
        Directory.CreateDirectory(_workDir);

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
        RebuildPreview();
    }

    public void SelectPage(int index)
    {
        if (index < 0 || index >= _pages.Count)
            return;

        _selectedPage = index;
        RebuildPreview();
    }

    public void SelectPrinter(string? printerName)
    {
        if (!string.IsNullOrWhiteSpace(printerName))
            _selectedPrinter = printerName;
    }

    public async Task PlanAsync(
        string userRequest,
        string safetyMode,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<SafetyMode>(
                safetyMode,
                ignoreCase: true,
                out var mode))
        {
            mode = SafetyMode.Smart;
        }

        var plan = await _planning.PlanAsync(
            userRequest,
            _paths,
            _selectedPrinter,
            mode,
            cancellationToken);

        _status = plan.Error is not null
            ? $"Plan lỗi: {plan.Error}"
            : $"AI plan: {plan.Decision} · confidence {plan.Confidence:P0}";

        RebuildPreview();
    }

    public void Clear()
    {
        _paths.Clear();
        _pages.Clear();
        _selectedPage = 0;
        _previewDataUrl = null;
        _printPath = null;
        _status = null;
        _planning.Clear();
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
    }

    public void PrintAll()
    {
        if (_pages.Count == 0)
        {
            _status = "Không có trang để in.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedPrinter))
        {
            _status = "Không tìm thấy máy in.";
            return;
        }

        var success = 0;
        for (var i = 0; i < _pages.Count; i++)
        {
            try
            {
                var page = _pages[i];
                var job = ResolveJob(page.SourcePath);
                var png = SourcePagePreview.RenderA4(
                    job, page.SourcePath, page.SourcePageIndex, dpi: 300);
                var path = Path.Combine(_workDir, $"print-{i:D4}.png");
                File.WriteAllBytes(path, png);

                var result = Submit(path);
                if (result.State == PrintSubmissionState.Failed)
                {
                    _status = $"Dừng ở trang {i + 1}/{_pages.Count}: {result.Error}";
                    return;
                }

                success++;
            }
            catch (Exception ex)
            {
                _status = $"Dừng ở trang {i + 1}/{_pages.Count}: {ex.Message}";
                return;
            }
        }

        _status = $"Đã gửi {success}/{_pages.Count} trang tới spooler.";
    }

    public DesktopState BuildState()
    {
        var printers = PrinterCapabilityProbe.Enumerate()
            .Select(p => new DesktopPrinter(p.Name, p.IsDefault, p.SupportsColor, p.CanDuplex))
            .ToArray();

        return new(
            Files: _paths.Select(InspectSafe).ToArray(),
            Pages: _pages,
            SelectedPage: _selectedPage,
            Printers: printers,
            SelectedPrinter: _selectedPrinter,
            PreviewDataUrl: _previewDataUrl,
            Status: _status,
            PlannerAvailable: _planning.IsAvailable,
            Plan: _planning.Current,
            CanPrint: _printPath is not null && !string.IsNullOrWhiteSpace(_selectedPrinter),
            CanPrintAll: _pages.Count > 0 && !string.IsNullOrWhiteSpace(_selectedPrinter));
    }

    private void RebuildPages()
    {
        _pages.Clear();

        foreach (var path in _paths)
        {
            try
            {
                var metadata = SourceInspector.Inspect(path);
                var count = metadata.Kind == SourceKind.Pdf
                    ? metadata.PageCount ?? 0
                    : 1;

                for (var page = 0; page < count; page++)
                {
                    _pages.Add(new(
                        GlobalIndex: _pages.Count,
                        SourcePath: path,
                        SourceName: Path.GetFileName(path),
                        SourcePageIndex: page,
                        PageLabel: metadata.Kind == SourceKind.Pdf
                            ? $"Trang {page + 1}/{count}"
                            : "Ảnh"));
                }
            }
            catch
            {
                // Per-file error is already surfaced through InspectSafe.
            }
        }
    }

    private void RebuildPreview()
    {
        if (_pages.Count == 0)
        {
            _previewDataUrl = null;
            _printPath = null;
            _status = _paths.Count == 0 ? null : "Không có trang hợp lệ để preview.";
            return;
        }

        try
        {
            var page = _pages[_selectedPage];
            var job = ResolveJob(page.SourcePath);
            var preview = SourcePagePreview.RenderA4(
                job, page.SourcePath, page.SourcePageIndex, dpi: 96);
            var printable = SourcePagePreview.RenderA4(
                job, page.SourcePath, page.SourcePageIndex, dpi: 300);

            _previewDataUrl = $"data:image/png;base64,{Convert.ToBase64String(preview)}";
            _printPath = Path.Combine(_workDir, "current-print.png");
            File.WriteAllBytes(_printPath, printable);
            _status = $"Preview {page.SourceName} · {page.PageLabel} sẵn sàng.";
        }
        catch (Exception ex)
        {
            _previewDataUrl = null;
            _printPath = null;
            _status = $"Không tạo được preview: {ex.Message}";
        }
    }

    private PrintSubmissionResult Submit(string path)
    {
        if (string.IsNullOrWhiteSpace(_selectedPrinter))
            return new(PrintSubmissionState.Failed, "", "", Error: "Không tìm thấy máy in.");

        var profile = _selectedPrinter.Contains("L3310", StringComparison.OrdinalIgnoreCase)
            ? PrinterDeviceProfile.EpsonL3310Calibrated
            : new PrinterDeviceProfile("default", _selectedPrinter);

        return WindowsSpoolerPrinter.SubmitA4Png(_selectedPrinter, path, profile);
    }

    private static string Describe(PrintSubmissionResult result) =>
        result.State == PrintSubmissionState.Failed
            ? $"In lỗi: {result.Error}"
            : result.JobId is int id
                ? $"Đã gửi tới spooler · Job #{id}"
                : "Đã gửi tới spooler.";

    private PrintJobSpec ResolveJob(string path) =>
        _planning.ResolveJobForSource(path) ?? CreateDefaultJob(path);

    private static PrintJobSpec CreateDefaultJob(string path) =>
        new(
            Path.GetFileName(path),
            [new SourceSpec(path)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 200,
                ItemHeightMm: 287,
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
    IReadOnlyList<DesktopPrinter> Printers,
    string? SelectedPrinter,
    string? PreviewDataUrl,
    string? Status,
    bool PlannerAvailable,
    DesktopPlanState? Plan,
    bool CanPrint,
    bool CanPrintAll);

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
