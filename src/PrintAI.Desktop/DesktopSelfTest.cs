using System.IO;
using System.Reflection;
using System.Text.Json;
using PrintAI.DocumentConversion;
using PrintAI.Domain;
using PrintAI.Rendering;
using PrintAI.SourceInspection;
using PrintAI.Windows.Printing;
using SkiaSharp;

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

internal static partial class DesktopSelfTest
{
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
            CheckRequestQueueLifecycle(checks, tempDirectory);
            CheckSpreadsheetPipeline(checks, tempDirectory);
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
        var desktopIndex = Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "index.html");

        var localWebIndex = Path.Combine(
            AppContext.BaseDirectory,
            "local-web",
            "index.html");

        var present =
            File.Exists(desktopIndex) &&
            File.Exists(localWebIndex);

        checks.Add(new(
            "ui-assets",
            present,
            present
                ? $"desktop={desktopIndex}; local-web={localWebIndex}"
                : "ui/index.html or local-web/index.html is missing from the package."));
    }

    private static void CheckRasterPipeline(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory)
    {
        var sourcePath = Path.Combine(
            tempDirectory,
            "tiny.png");

        WriteSmokePng(sourcePath);

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

    private static void WriteSmokePng(string path)
    {
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRect(8, 8, 16, 16, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        if (data is null)
            throw new InvalidOperationException("Could not encode self-test PNG.");

        File.WriteAllBytes(path, data.ToArray());
    }

    private static void CheckRequestQueueLifecycle(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory)
    {
        var first = Path.Combine(tempDirectory, "queue-a.png");
        var second = Path.Combine(tempDirectory, "queue-b.png");
        WriteSmokePng(first);
        WriteSmokePng(second);

        var session = new DesktopSession();
        session.AddPaths([first, second]);
        session.UpsertQueuedRequest(
            id: null,
            request: "In hai ảnh này",
            mode: "Smart",
            sourceIndexes: [0, 1]);

        var before = session.BuildState();
        if (before.RequestQueue.Count != 1 ||
            before.RequestQueue[0].SourcePaths.Count != 2)
        {
            checks.Add(new(
                "request-queue-lifecycle",
                false,
                "Could not create a two-source queued request."));
            return;
        }

        var queueId = before.RequestQueue[0].Id;
        session.UpsertQueuedRequest(
            id: queueId,
            request: "Chỉ in ảnh thứ hai",
            mode: "Safe",
            sourceIndexes: [1]);

        var afterEdit = session.BuildState();
        if (afterEdit.RequestQueue.Count != 1 ||
            afterEdit.RequestQueue[0].Request != "Chỉ in ảnh thứ hai" ||
            afterEdit.RequestQueue[0].Mode != "Safe" ||
            afterEdit.RequestQueue[0].SourcePaths.Count != 1 ||
            !string.Equals(
                afterEdit.RequestQueue[0].SourcePaths[0],
                second,
                StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new(
                "request-queue-lifecycle",
                false,
                "Editing a queued request did not preserve the intended source set."));
            return;
        }

        session.UpsertQueuedRequest(
            id: null,
            request: "Yêu cầu thứ hai",
            mode: "Smart",
            sourceIndexes: [0]);

        session.RemoveQueuedRequest(queueId);
        var afterDelete = session.BuildState();
        if (afterDelete.RequestQueue.Count != 1 ||
            afterDelete.RequestQueue[0].Order != 1 ||
            afterDelete.RequestQueue[0].Request != "Yêu cầu thứ hai")
        {
            checks.Add(new(
                "request-queue-lifecycle",
                false,
                "Deleting a queued request did not compact queue order."));
            return;
        }

        session.RemoveQueuedRequest(afterDelete.RequestQueue[0].Id);

        session.UpsertQueuedRequest(
            id: null,
            request: "In hai ảnh này",
            mode: "Smart",
            sourceIndexes: [0, 1]);

        session.RemoveSource(0);
        var afterRemove = session.BuildState();

        if (afterRemove.RequestQueue.Count != 1 ||
            afterRemove.RequestQueue[0].SourcePaths.Count != 1 ||
            !string.Equals(
                afterRemove.RequestQueue[0].SourcePaths[0],
                second,
                StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new(
                "request-queue-lifecycle",
                false,
                "Removing a source left stale queue source references."));
            return;
        }

        session.Clear();
        var afterClear = session.BuildState();
        var passed =
            afterClear.Files.Count == 0 &&
            afterClear.RequestQueue.Count == 0;

        checks.Add(new(
            "request-queue-lifecycle",
            passed,
            passed
                ? "Queue create/edit/delete/source-removal/clear lifecycle passed."
                : "Clearing sources left stale queued requests."));
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
