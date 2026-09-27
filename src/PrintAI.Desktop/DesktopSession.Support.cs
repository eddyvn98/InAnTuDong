using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using PrintAI.DocumentConversion;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

internal sealed record DesktopSupportPrinter(
    string Name,
    bool IsDefault,
    bool SupportsColor,
    bool CanDuplex,
    string ProfileId,
    bool ProfilePhysicallyVerified);

internal sealed record DesktopSupportReport(
    string SchemaVersion,
    DateTimeOffset CreatedAt,
    string ProductVersion,
    string OperatingSystem,
    string Runtime,
    int SourcesLoaded,
    int SourcePages,
    bool OfficeConversionAvailable,
    bool AiPlannerConfigured,
    IReadOnlyList<DesktopSupportPrinter> Printers,
    IReadOnlyList<string> Scanners,
    DesktopReadinessView Readiness);

public sealed partial class DesktopSession
{
    public void ExportSupportReport(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var dialog = new SaveFileDialog
        {
            Title = "Xuất Print AI support report",
            FileName =
                $"PrintAI-support-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExt = ".json",
            Filter = "JSON report|*.json"
        };

        if (dialog.ShowDialog(owner) != true)
            return;

        var state = BuildState();
        var report = new DesktopSupportReport(
            SchemaVersion: "1.0",
            CreatedAt: DateTimeOffset.Now,
            ProductVersion: Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "unknown",
            OperatingSystem: RuntimeInformation.OSDescription,
            Runtime: RuntimeInformation.RuntimeIdentifier,
            SourcesLoaded: _paths.Count,
            SourcePages: _pages.Count,
            OfficeConversionAvailable:
                OfficeDocumentConverter.FindLibreOfficeExecutable() is not null,
            AiPlannerConfigured: state.Planner.Configured,
            Printers: state.Printers
                .Select(printer =>
                {
                    var profile =
                        PrinterProfileCatalog.Resolve(printer.Name);

                    return new DesktopSupportPrinter(
                        printer.Name,
                        printer.IsDefault,
                        printer.SupportsColor,
                        printer.CanDuplex,
                        profile.Id,
                        profile.IsPhysicallyVerified);
                })
                .ToArray(),
            Scanners: state.Scanners
                .Select(scanner => scanner.Name)
                .ToArray(),
            Readiness: state.Readiness);

        File.WriteAllText(
            dialog.FileName,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        _status =
            "Đã xuất support report. Report không chứa API key, " +
            "đường dẫn source hoặc lịch sử job.";
    }
}
