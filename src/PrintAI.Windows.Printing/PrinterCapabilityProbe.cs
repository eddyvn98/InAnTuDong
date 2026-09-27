using System.Drawing.Printing;

namespace PrintAI.Windows.Printing;

public static class PrinterCapabilityProbe
{
    public static IReadOnlyList<PrinterCapabilitySnapshot> Enumerate()
    {
        var result = new List<PrinterCapabilitySnapshot>();

        foreach (string printerName in PrinterSettings.InstalledPrinters)
            result.Add(Inspect(printerName));

        return result;
    }

    public static PrinterCapabilitySnapshot Inspect(string printerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        try
        {
            var settings = new PrinterSettings { PrinterName = printerName };
            if (!settings.IsValid)
                return Failed(printerName, "Windows reports this printer configuration as invalid.");

            var papers = ReadPaperSizes(settings);
            var resolutions = ReadResolutions(settings);
            var a4 = papers.FirstOrDefault(IsA4);
            var a4Page = TryReadA4Page(settings, a4);

            return new(
                Name: settings.PrinterName,
                IsDefault: settings.IsDefaultPrinter,
                IsValid: true,
                SupportsColor: settings.SupportsColor,
                CanDuplex: settings.CanDuplex,
                PaperSizes: papers,
                Resolutions: resolutions,
                A4Portrait: a4Page);
        }
        catch (Exception ex) when (
            ex is InvalidPrinterException or ArgumentException or SystemException)
        {
            return Failed(printerName, ex.Message);
        }
    }

    public static double HundredthsInchToMm(float value) => value * 0.254d;

    private static List<PaperCapability> ReadPaperSizes(PrinterSettings settings)
    {
        var result = new List<PaperCapability>();

        foreach (PaperSize paper in settings.PaperSizes)
        {
            result.Add(new(
                Name: paper.PaperName,
                Kind: paper.Kind.ToString(),
                RawKind: paper.RawKind,
                WidthMm: HundredthsInchToMm(paper.Width),
                HeightMm: HundredthsInchToMm(paper.Height)));
        }

        return result;
    }

    private static List<ResolutionCapability> ReadResolutions(PrinterSettings settings)
    {
        var result = new List<ResolutionCapability>();

        foreach (PrinterResolution resolution in settings.PrinterResolutions)
        {
            result.Add(new(
                Kind: resolution.Kind.ToString(),
                X: resolution.X,
                Y: resolution.Y));
        }

        return result;
    }

    private static PageCapability? TryReadA4Page(
        PrinterSettings settings,
        PaperCapability? a4Capability)
    {
        if (a4Capability is null)
            return null;

        var actualPaper = settings.PaperSizes
            .Cast<PaperSize>()
            .FirstOrDefault(p => p.RawKind == a4Capability.RawKind);

        if (actualPaper is null)
            return null;

        var page = new PageSettings(settings)
        {
            Landscape = false,
            PaperSize = actualPaper
        };

        var printable = page.PrintableArea;

        return new(
            PaperWidthMm: HundredthsInchToMm(actualPaper.Width),
            PaperHeightMm: HundredthsInchToMm(actualPaper.Height),
            PrintableXmm: HundredthsInchToMm(printable.X),
            PrintableYmm: HundredthsInchToMm(printable.Y),
            PrintableWidthMm: HundredthsInchToMm(printable.Width),
            PrintableHeightMm: HundredthsInchToMm(printable.Height),
            HardMarginXmm: HundredthsInchToMm(page.HardMarginX),
            HardMarginYmm: HundredthsInchToMm(page.HardMarginY));
    }

    private static bool IsA4(PaperCapability paper)
    {
        if (paper.Kind.Equals(PaperKind.A4.ToString(), StringComparison.OrdinalIgnoreCase))
            return true;

        var width = Math.Min(paper.WidthMm, paper.HeightMm);
        var height = Math.Max(paper.WidthMm, paper.HeightMm);

        return Math.Abs(width - 210) <= 2 && Math.Abs(height - 297) <= 2;
    }

    private static PrinterCapabilitySnapshot Failed(string name, string error) =>
        new(
            Name: name,
            IsDefault: false,
            IsValid: false,
            SupportsColor: false,
            CanDuplex: false,
            PaperSizes: [],
            Resolutions: [],
            A4Portrait: null,
            Error: error);
}
