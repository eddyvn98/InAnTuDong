namespace PrintAI.Windows.Printing;

public sealed record PrinterDeviceProfile(
    string Id,
    string PrinterQuery,
    double ScaleX = 1.0,
    double ScaleY = 1.0,
    double OffsetXMm = 0,
    double OffsetYMm = 0,
    bool IsPhysicallyVerified = false)
{
    public static PrinterDeviceProfile EpsonL3310Calibrated { get; } =
        new(
            Id: "epson-l3310-calibrated-2026-09-27",
            PrinterQuery: "L3310",
            ScaleX: 1.0,
            ScaleY: 1.0,
            OffsetXMm: 0,
            OffsetYMm: 0,
            IsPhysicallyVerified: true);

    public static PrinterDeviceProfile EpsonL3316Default { get; } =
        new(
            Id: "epson-l3316-default",
            PrinterQuery: "L3316");

    public static PrinterDeviceProfile EpsonL3210Default { get; } =
        new(
            Id: "epson-l3210-default",
            PrinterQuery: "L3210");

    public static PrinterDeviceProfile EpsonL3250Default { get; } =
        new(
            Id: "epson-l3250-default",
            PrinterQuery: "L3250");
}
