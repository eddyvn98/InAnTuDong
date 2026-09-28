using System.Drawing.Printing;
using PrintAI.Domain;
using PrintAI.Windows.Printing;
using Xunit;

namespace PrintAI.Windows.Tests;

public sealed class WindowsSpoolerDuplexTests
{
    [Theory]
    [InlineData(DuplexMode.Off, false, Duplex.Simplex)]
    [InlineData(DuplexMode.LongEdge, false, Duplex.Vertical)]
    [InlineData(DuplexMode.ShortEdge, false, Duplex.Horizontal)]
    [InlineData(DuplexMode.LongEdge, true, Duplex.Horizontal)]
    [InlineData(DuplexMode.ShortEdge, true, Duplex.Vertical)]
    public void DuplexMapping_RespectsBindingEdgeAndOrientation(
        DuplexMode mode,
        bool landscape,
        Duplex expected)
    {
        Assert.Equal(
            expected,
            WindowsSpoolerPrinter.ResolveDuplexSetting(mode, landscape));
    }

    [Theory]
    [InlineData(ColorMode.Color, true, true)]
    [InlineData(ColorMode.Color, false, false)]
    [InlineData(ColorMode.Grayscale, true, false)]
    [InlineData(ColorMode.Grayscale, false, false)]
    public void ColorMapping_RespectsIntentAndPrinterCapability(
        ColorMode mode,
        bool printerSupportsColor,
        bool expected)
    {
        Assert.Equal(
            expected,
            WindowsSpoolerPrinter.ResolveColorSetting(
                mode,
                printerSupportsColor));
    }

    [Fact]
    public void SubmitPages_RejectsEmptyBatchBeforePrinterAccess()
    {
        var result = WindowsSpoolerPrinter.SubmitPages(
            "not-a-real-printer",
            [],
            210,
            297,
            landscape: false);

        Assert.Equal(PrintSubmissionState.Failed, result.State);
        Assert.Contains("At least one", result.Error!);
    }

    [Fact]
    public void SubmitPages_RejectsUnsupportedRotationBeforePrinterAccess()
    {
        var path = Path.GetTempFileName();

        try
        {
            var result = WindowsSpoolerPrinter.SubmitPages(
                "not-a-real-printer",
                [new PrintablePage(path, 90)],
                210,
                297,
                landscape: false);

            Assert.Equal(PrintSubmissionState.Failed, result.State);
            Assert.Contains("0 or 180", result.Error!);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
