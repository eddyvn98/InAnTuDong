namespace PrintAI.Scanning;

public enum ScanColorMode
{
    Color,
    Grayscale,
    BlackAndWhite
}

public sealed record ScannerDevice(
    string Id,
    string Name,
    string Adapter);

public sealed record ScanRequest(
    string OutputDirectory,
    string? DeviceId = null,
    int Dpi = 300,
    ScanColorMode ColorMode = ScanColorMode.Color);

public sealed record ScanResult(
    string Path,
    ScannerDevice Device,
    int Dpi,
    ScanColorMode ColorMode);

public interface IScannerAdapter
{
    Task<IReadOnlyList<ScannerDevice>> EnumerateAsync(
        CancellationToken cancellationToken = default);

    Task<ScanResult> ScanAsync(
        ScanRequest request,
        CancellationToken cancellationToken = default);
}
