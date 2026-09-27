namespace PrintAI.Scanning;

public enum ScanColorMode
{
    Color,
    Grayscale,
    BlackAndWhite
}

public enum ScanOutputFormat
{
    Png,
    Pdf
}

public sealed record ScannerDevice(
    string Id,
    string Name);

public sealed record ScanCaptureSettings(
    int Dpi = 300,
    ScanColorMode ColorMode = ScanColorMode.Color)
{
    public void Validate()
    {
        if (Dpi is < 75 or > 1200)
            throw new ArgumentOutOfRangeException(nameof(Dpi), "Scan DPI must be between 75 and 1200.");
    }
}

public sealed record ScanProcessingSettings(
    bool AutoCrop = true,
    bool AutoDeskew = true,
    double MaxDeskewDegrees = 5,
    byte WhiteThreshold = 238)
{
    public void Validate()
    {
        if (MaxDeskewDegrees is < 0 or > 15)
            throw new ArgumentOutOfRangeException(
                nameof(MaxDeskewDegrees),
                "Max deskew angle must be between 0 and 15 degrees.");
    }
}

public sealed record ScanProcessResult(
    string OutputPath,
    int PixelWidth,
    int PixelHeight,
    bool Cropped,
    double DeskewDegrees);

public interface IScannerAdapter
{
    IReadOnlyList<ScannerDevice> Enumerate();

    Task<string> ScanPageAsync(
        string scannerId,
        ScanCaptureSettings settings,
        string outputPath,
        CancellationToken cancellationToken = default);
}
