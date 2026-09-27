using PrintAI.Windows.Printing;
using Xunit;

namespace PrintAI.Windows.Tests;

public sealed class PrinterProbeTests
{
    [Fact]
    public void HundredthsInchToMm_ConvertsPrinterUnits()
    {
        Assert.Equal(25.4, PrinterCapabilityProbe.HundredthsInchToMm(100), 6);
        Assert.Equal(210.058, PrinterCapabilityProbe.HundredthsInchToMm(827), 3);
    }

    [Fact]
    public void Matcher_FindsEpsonL3310ByModelToken()
    {
        PrinterCapabilitySnapshot[] printers =
        [
            Create("Microsoft Print to PDF"),
            Create("EPSON L3310 Series")
        ];

        var match = PrinterMatcher.FindBest(printers, "L3310");

        Assert.NotNull(match);
        Assert.Equal("EPSON L3310 Series", match.Name);
    }

    [Fact]
    public void Matcher_PrefersExactName()
    {
        PrinterCapabilitySnapshot[] printers =
        [
            Create("EPSON L3310 Series Copy 1"),
            Create("EPSON L3310 Series")
        ];

        var match = PrinterMatcher.FindBest(printers, "EPSON L3310 Series");

        Assert.NotNull(match);
        Assert.Equal("EPSON L3310 Series", match.Name);
    }

    private static PrinterCapabilitySnapshot Create(string name) =>
        new(
            name,
            IsDefault: false,
            IsValid: true,
            SupportsColor: true,
            CanDuplex: false,
            PaperSizes: [],
            Resolutions: [],
            A4Portrait: null);
}
