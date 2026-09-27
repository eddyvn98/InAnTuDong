using System.IO;
using PrintAI.DocumentConversion;
using PrintAI.ReleaseReadiness;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void RefreshReadiness()
    {
        _status = "Đã làm mới kiểm tra hệ thống.";
    }

    private DesktopReadinessView BuildReadiness(
        int printerCount,
        int scannerCount,
        bool plannerConfigured)
    {
        var report = ReleaseReadinessEvaluator.Evaluate(
            new(
                WorkDirectoryWritable: IsWorkDirectoryWritable(),
                PrinterCount: printerCount,
                SelectedPrinterVerified: IsVerifiedPrinter(),
                ScannerCount: scannerCount,
                LibreOfficeAvailable:
                    OfficeDocumentConverter.FindLibreOfficeExecutable() is not null,
                AiPlannerConfigured: plannerConfigured));

        return new(
            report.ReadyForCorePrinting,
            report.PassCount,
            report.WarningCount,
            report.FailureCount,
            report.Checks
                .Select(check => new DesktopReadinessCheck(
                    check.Id,
                    check.Name,
                    check.State.ToString(),
                    check.Detail))
                .ToArray());
    }

    private bool IsWorkDirectoryWritable()
    {
        var probe = Path.Combine(
            _workDir,
            $".write-probe-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(probe))
                    File.Delete(probe);
            }
            catch
            {
            }

            return false;
        }
    }
}
