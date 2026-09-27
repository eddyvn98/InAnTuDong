using PrintAI.ReleaseReadiness;
using Xunit;

namespace PrintAI.Tests;

public sealed class ReleaseReadinessEvaluatorTests
{
    [Fact]
    public void Evaluate_CoreReady_WhenStorageAndPrinterAreAvailable()
    {
        var report = ReleaseReadinessEvaluator.Evaluate(
            new(
                WorkDirectoryWritable: true,
                PrinterCount: 1,
                SelectedPrinterVerified: true,
                ScannerCount: 1,
                LibreOfficeAvailable: true,
                AiPlannerConfigured: true));

        Assert.True(report.ReadyForCorePrinting);
        Assert.Equal(6, report.PassCount);
        Assert.Equal(0, report.WarningCount);
        Assert.Equal(0, report.FailureCount);
    }

    [Fact]
    public void Evaluate_MissingOptionalCapabilities_AreWarnings()
    {
        var report = ReleaseReadinessEvaluator.Evaluate(
            new(
                WorkDirectoryWritable: true,
                PrinterCount: 1,
                SelectedPrinterVerified: false,
                ScannerCount: 0,
                LibreOfficeAvailable: false,
                AiPlannerConfigured: false));

        Assert.True(report.ReadyForCorePrinting);
        Assert.Equal(4, report.WarningCount);
        Assert.Equal(0, report.FailureCount);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void Evaluate_CoreMissingCapability_IsFailure(
        bool storageWritable,
        int printerCount)
    {
        var report = ReleaseReadinessEvaluator.Evaluate(
            new(
                WorkDirectoryWritable: storageWritable,
                PrinterCount: printerCount,
                SelectedPrinterVerified: false,
                ScannerCount: 0,
                LibreOfficeAvailable: false,
                AiPlannerConfigured: false));

        Assert.False(report.ReadyForCorePrinting);
        Assert.True(report.FailureCount >= 1);
    }
}
