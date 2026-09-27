using PrintAI.Scanning;
using PrintAI.Windows.Scanning;

namespace PrintAI.Desktop;

public sealed class DesktopScannerSession
{
    private readonly IScannerAdapter _adapter =
        new WiaScannerAdapter();

    private IReadOnlyList<ScannerDevice> _devices = [];

    public IReadOnlyList<DesktopScanner> Devices =>
        _devices
            .Select(device =>
                new DesktopScanner(
                    device.Id,
                    device.Name,
                    device.Adapter))
            .ToArray();

    public string? Status { get; private set; }

    public async Task RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _devices = await _adapter.EnumerateAsync(
                cancellationToken);

            Status = _devices.Count == 0
                ? "Không tìm thấy scanner WIA."
                : $"Tìm thấy {_devices.Count} scanner.";
        }
        catch (ScannerUnavailableException ex)
        {
            _devices = [];
            Status = ex.Message;
        }
    }

    public async Task<ScanResult> ScanAsync(
        string outputDirectory,
        string? deviceId,
        int dpi,
        ScanColorMode colorMode,
        CancellationToken cancellationToken = default)
    {
        Status = "Đang scan…";

        try
        {
            var result = await _adapter.ScanAsync(
                new ScanRequest(
                    OutputDirectory: outputDirectory,
                    DeviceId: string.IsNullOrWhiteSpace(deviceId)
                        ? null
                        : deviceId,
                    Dpi: dpi,
                    ColorMode: colorMode),
                cancellationToken);

            Status =
                $"Scan xong từ {result.Device.Name} · {dpi} DPI.";

            return result;
        }
        catch (Exception ex)
        {
            Status = $"Scan lỗi: {ex.Message}";
            throw;
        }
    }
}
