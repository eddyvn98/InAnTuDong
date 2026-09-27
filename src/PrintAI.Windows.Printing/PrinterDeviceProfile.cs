namespace PrintAI.Windows.Printing;

public sealed record PrinterDeviceProfile(
    string Id,
    string PrinterQuery,
    double ScaleX = 1.0,
    double ScaleY = 1.0,
    double OffsetXMm = 0,
    double OffsetYMm = 0)
{
    public static PrinterDeviceProfile EpsonL3310Calibrated { get; } =
        new(
            Id: "epson-l3310-calibrated-2026-09-27",
            PrinterQuery: "L3310",
            ScaleX: 1.0,
            ScaleY: 1.0,
            OffsetXMm: 0,
            OffsetYMm: 0);
}
