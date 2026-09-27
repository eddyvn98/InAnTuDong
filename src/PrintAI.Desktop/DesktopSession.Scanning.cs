using PrintAI.Scanning;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    private readonly DesktopScannerSession _scanner = new();

    public async Task RefreshScannersAsync(
        CancellationToken cancellationToken = default)
    {
        await _scanner.RefreshAsync(cancellationToken);
        _status = _scanner.Status;
    }

    public async Task ScanAsync(
        string? deviceId,
        int dpi,
        string colorMode,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ScanColorMode>(
                colorMode,
                ignoreCase: true,
                out var parsedMode))
        {
            throw new ArgumentException(
                "Scan color mode must be Color, Grayscale or BlackAndWhite.");
        }

        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyPictures),
            "PrintAI",
            "Scans");

        var result = await _scanner.ScanAsync(
            outputDirectory,
            deviceId,
            dpi,
            parsedMode,
            cancellationToken);

        AddPaths([result.Path]);
        _status = _scanner.Status;

        _history.Append(new(
            DateTimeOffset.Now,
            Action: "scan",
            Status: "Completed",
            Printer: null,
            JobName: Path.GetFileName(result.Path),
            Detail:
                $"{result.Device.Name} · {result.Dpi} DPI · " +
                result.ColorMode));
    }
}
