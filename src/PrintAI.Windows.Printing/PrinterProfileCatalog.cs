namespace PrintAI.Windows.Printing;

public static class PrinterProfileCatalog
{
    private static readonly PrinterDeviceProfile[] Profiles =
    [
        PrinterDeviceProfile.EpsonL3310Calibrated,
        PrinterDeviceProfile.EpsonL3316Default,
        PrinterDeviceProfile.EpsonL3210Default,
        PrinterDeviceProfile.EpsonL3250Default
    ];

    public static IReadOnlyList<PrinterDeviceProfile> Known => Profiles;

    public static PrinterDeviceProfile Resolve(string printerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        var match = Profiles
            .OrderByDescending(profile => profile.PrinterQuery.Length)
            .FirstOrDefault(profile =>
                printerName.Contains(
                    profile.PrinterQuery,
                    StringComparison.OrdinalIgnoreCase));

        return match ?? new PrinterDeviceProfile(
            Id: "generic-a4-default",
            PrinterQuery: printerName,
            ManualDuplex: ManualDuplexProfile.UnverifiedDefault);
    }
}
