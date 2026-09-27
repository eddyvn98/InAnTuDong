using System.IO;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Win32;
using PrintAI.Domain;
using PrintAI.Rendering;
using PrintAI.SourceInspection;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

public sealed class DesktopSession
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

    private readonly List<string> _paths = [];
    private readonly string _workDir;
    private string? _selectedPrinter;
    private string? _status;
    private string? _previewDataUrl;
    private string? _printPath;

    public DesktopSession()
    {
        _workDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintAI",
            "work");
        Directory.CreateDirectory(_workDir);

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
            Title = "Chọn thư mục ảnh/PDF",
            Multiselect = false
        };

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
        _previewDataUrl = null;
        _printPath = null;
        _status = null;
    }

    public void Print()
    {
        if (_printPath is null)
        {
            _status = "Chưa có preview raster có thể in.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedPrinter))
        {
            _status = "Không tìm thấy máy in.";
            return;
        }

        var profile = _selectedPrinter.Contains(
            "L3310",
            StringComparison.OrdinalIgnoreCase)
            ? PrinterDeviceProfile.EpsonL3310Calibrated
            : new PrinterDeviceProfile("default", _selectedPrinter);

        var result = WindowsSpoolerPrinter.SubmitA4Png(
            _selectedPrinter,
            _printPath,
            profile);

        _status = result.State == PrintSubmissionState.Failed
            ? $"In lỗi: {result.Error}"
            : result.JobId is int id
                ? $"Đã gửi tới spooler · Job #{id}"
                : "Đã gửi tới spooler.";
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

        var files = _paths.Select(InspectSafe).ToArray();

        return new(
            Files: files,
            Printers: printers,
            SelectedPrinter: _selectedPrinter,
            PreviewDataUrl: _previewDataUrl,
            Status: _status,
            CanPrint: _printPath is not null &&
                      !string.IsNullOrWhiteSpace(_selectedPrinter));
    }

    private void RebuildPreview()
    {
        var rasterPath = _paths.FirstOrDefault(IsRaster);
        if (rasterPath is null)
        {
            _previewDataUrl = null;
            _printPath = null;
            _status = _paths.Count == 0
                ? null
                : "PDF đã inspect được nhưng raster preview PDF chưa có ở milestone này.";
            return;
        }

        try
        {
            var job = CreateDefaultJob(rasterPath);
            var preview = RasterFilePreview.RenderA4(job, rasterPath, dpi: 96);
            var printable = RasterFilePreview.RenderA4(job, rasterPath, dpi: 300);

            _previewDataUrl =
                $"data:image/png;base64,{Convert.ToBase64String(preview)}";

            _printPath = Path.Combine(_workDir, "current-print.png");
            File.WriteAllBytes(_printPath, printable);
            _status = "Preview sẵn sàng. Kiểm tra rồi bấm Print.";
        }
        catch (Exception ex)
        {
            _previewDataUrl = null;
            _printPath = null;
            _status = $"Không tạo được preview: {ex.Message}";
        }
    }

    private static PrintJobSpec CreateDefaultJob(string path) =>
        new(
            JobName: Path.GetFileName(path),
            Sources: [new SourceSpec(path)],
            Paper: new PaperSpec(),
            Layout: new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 200,
                ItemHeightMm: 287,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            Print: new PrintSettings(),
            Policy: new PolicySpec(PreviewPolicy.Required));

    private static DesktopFile InspectSafe(string path)
    {
        try
        {
            var metadata = SourceInspector.Inspect(path);
            return new(
                Path.GetFileName(path),
                path,
                metadata.Kind.ToString(),
                metadata.PixelWidth,
                metadata.PixelHeight,
                metadata.PageCount,
                null);
        }
        catch (Exception ex)
        {
            return new(
                Path.GetFileName(path),
                path,
                "Error",
                null,
                null,
                null,
                ex.Message);
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

            foreach (var file in Directory.EnumerateFiles(
                path,
                "*.*",
                SearchOption.TopDirectoryOnly))
            {
                if (Supported(file))
                    yield return Path.GetFullPath(file);
            }
        }
    }

    private static bool Supported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private static bool IsRaster(string path) =>
        Path.GetExtension(path) is ".jpg" or ".jpeg" or ".png" ||
        Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase);
}

public sealed record DesktopState(
    IReadOnlyList<DesktopFile> Files,
    IReadOnlyList<DesktopPrinter> Printers,
    string? SelectedPrinter,
    string? PreviewDataUrl,
    string? Status,
    bool CanPrint);

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
