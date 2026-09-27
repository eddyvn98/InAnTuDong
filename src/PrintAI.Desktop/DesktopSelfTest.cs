using System.Reflection;
using System.Text.Json;
using PrintAI.DocumentConversion;
using PrintAI.Domain;
using PrintAI.Rendering;
using PrintAI.SourceInspection;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

internal sealed record DesktopSelfTestCheck(
    string Name,
    bool Passed,
    string Detail);

internal sealed record DesktopSelfTestReport(
    bool Success,
    string Version,
    string Runtime,
    IReadOnlyList<DesktopSelfTestCheck> Checks);

internal static class DesktopSelfTest
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZlWQAAAAASUVORK5CYII=";

    public static int Run(string? outputPath)
    {
        var checks = new List<DesktopSelfTestCheck>();
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"printai-selftest-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        try
        {
            CheckUiAssets(checks);
            CheckRasterPipeline(checks, tempDirectory);
            CheckPrinterProbe(checks);
            CheckOfficeDiscovery(checks);
        }
        catch (Exception ex)
        {
            checks.Add(new(
                "self-test",
                false,
                ex.ToString()));
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
            }
        }

        var report = new DesktopSelfTestReport(
            Success: checks.All(check => check.Passed),
            Version: Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "unknown",
            Runtime: System.Runtime.InteropServices
                .RuntimeInformation
                .RuntimeIdentifier,
            Checks: checks);

        WriteReport(report, outputPath);
        return report.Success ? 0 : 1;
    }

    private static void CheckUiAssets(
        ICollection<DesktopSelfTestCheck> checks)
    {
        var indexPath = Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "index.html");

        checks.Add(new(
            "ui-assets",
            File.Exists(indexPath),
            File.Exists(indexPath)
                ? indexPath
                : "ui/index.html is missing from the package."));
    }

    private static void CheckRasterPipeline(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory)
    {
        var sourcePath = Path.Combine(
            tempDirectory,
            "tiny.png");

        File.WriteAllBytes(
            sourcePath,
            Convert.FromBase64String(TinyPngBase64));

        var metadata = SourceInspector.Inspect(sourcePath);
        if (metadata.Kind != SourceKind.Png ||
            metadata.PixelWidth is null ||
            metadata.PixelHeight is null)
        {
            checks.Add(new(
                "raster-pipeline",
                false,
                "PNG inspection did not return expected raster metadata."));
            return;
        }

        var job = new PrintJobSpec(
            "self-test",
            [new SourceSpec(sourcePath)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 20,
                ItemHeightMm: 20,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            new PrintSettings(),
            new PolicySpec(PreviewPolicy.Required));

        var rendered = SourceJobRenderer.RenderA4(
            job,
            sourcePath,
            sourcePageIndex: 0,
            outputPageIndex: 0,
            dpi: 96);

        checks.Add(new(
            "raster-pipeline",
            rendered.Length > 100,
            $"PNG {metadata.PixelWidth}x{metadata.PixelHeight} -> A4 preview {rendered.Length} bytes."));
    }

    private static void CheckPrinterProbe(
        ICollection<DesktopSelfTestCheck> checks)
    {
        var printers = PrinterCapabilityProbe.Enumerate();

        checks.Add(new(
            "printer-probe",
            true,
            $"Windows printer probe completed; {printers.Count} printer(s) visible."));
    }

    private static void CheckOfficeDiscovery(
        ICollection<DesktopSelfTestCheck> checks)
    {
        var converter =
            OfficeDocumentConverter.FindLibreOfficeExecutable();

        checks.Add(new(
            "office-discovery",
            true,
            converter is null
                ? "LibreOffice not installed; Office import remains optional."
                : $"LibreOffice found: {converter}"));
    }

    private static void WriteReport(
        DesktopSelfTestReport report,
        string? outputPath)
    {
        var path = string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(
                Environment.CurrentDirectory,
                "PrintAI-selftest.json")
            : Path.GetFullPath(outputPath);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }
}
