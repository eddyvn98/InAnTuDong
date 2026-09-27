using System.IO;
using PrintAI.Domain;
using PrintAI.History;
using PrintAI.Rendering;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void PrintCurrent()
    {
        if (_printPath is null)
        {
            _status = "Chưa có trang preview có thể in.";
            return;
        }

        var page = CurrentPage()
            ?? throw new InvalidOperationException("Không có trang đang chọn.");

        var job = CurrentJob(page);
        var result = Submit(_printPath);

        _status = Describe(result);
        RecordPrint("print-page", result, job);
    }

    public void PrintJob()
    {
        var page = CurrentPage();
        if (page is null)
        {
            _status = "Không có trang để in.";
            return;
        }

        var job = CurrentJob(page);
        var pageCount = SourceJobRenderer.GetOutputPageCount(job);
        var submitted = 0;

        for (var outputPage = 0;
             outputPage < pageCount;
             outputPage++)
        {
            try
            {
                var png = RequiresMixedRenderer(job)
                    ? SourceJobRenderer.RenderMixedA4(
                        job,
                        outputPage,
                        dpi: 300)
                    : SourceJobRenderer.RenderA4(
                        job,
                        page.SourcePath,
                        page.SourcePageIndex,
                        outputPage,
                        dpi: 300);

                var path = Path.Combine(
                    _workDir,
                    $"job-{outputPage:D4}.png");

                File.WriteAllBytes(path, png);

                var result = Submit(path);
                if (result.State == PrintSubmissionState.Failed)
                {
                    _status =
                        $"Dừng ở output {outputPage + 1}/{pageCount}: " +
                        result.Error;

                    RecordPrint("print-job", result, job);
                    return;
                }

                submitted++;
            }
            catch (Exception ex)
            {
                _status =
                    $"Dừng ở output {outputPage + 1}/{pageCount}: " +
                    ex.Message;

                _history.Append(new(
                    DateTimeOffset.Now,
                    "print-job",
                    "Failed",
                    _lastRequest,
                    _selectedPrinter,
                    job.JobName,
                    ex.Message));

                return;
            }
        }

        _status =
            $"Đã gửi {submitted}/{pageCount} output page tới spooler.";

        _history.Append(new(
            DateTimeOffset.Now,
            "print-job",
            "Submitted",
            _lastRequest,
            _selectedPrinter,
            job.JobName,
            _status));
    }

    public void PrintAllSources()
    {
        var submitted = 0;

        foreach (var page in _pages)
        {
            try
            {
                var job = CreateDefaultJob(page.SourcePath);
                var png = SourceJobRenderer.RenderA4(
                    job,
                    page.SourcePath,
                    page.SourcePageIndex,
                    0,
                    dpi: 300);

                var path = Path.Combine(
                    _workDir,
                    $"source-{submitted:D4}.png");

                File.WriteAllBytes(path, png);

                var result = Submit(path);
                if (result.State == PrintSubmissionState.Failed)
                {
                    _status =
                        $"Dừng ở source page {submitted + 1}/{_pages.Count}: " +
                        result.Error;
                    return;
                }

                submitted++;
            }
            catch (Exception ex)
            {
                _status =
                    $"Dừng ở source page {submitted + 1}/{_pages.Count}: " +
                    ex.Message;
                return;
            }
        }

        _status =
            $"Đã gửi {submitted}/{_pages.Count} source page tới spooler.";
    }

    private PrintSubmissionResult Submit(string path)
    {
        if (string.IsNullOrWhiteSpace(_selectedPrinter))
        {
            return new(
                PrintSubmissionState.Failed,
                "",
                "",
                Error: "Không tìm thấy máy in.");
        }

        var profile = IsVerifiedPrinter()
            ? PrinterDeviceProfile.EpsonL3310Calibrated
            : new PrinterDeviceProfile(
                "default",
                _selectedPrinter);

        return WindowsSpoolerPrinter.SubmitA4Png(
            _selectedPrinter,
            path,
            profile);
    }

    private void RecordPrint(
        string action,
        PrintSubmissionResult result,
        PrintJobSpec job) =>
        _history.Append(new JobHistoryEntry(
            DateTimeOffset.Now,
            action,
            result.State.ToString(),
            _lastRequest,
            _selectedPrinter,
            job.JobName,
            result.Error ?? result.JobId?.ToString()));

    private static string Describe(PrintSubmissionResult result) =>
        result.State == PrintSubmissionState.Failed
            ? $"In lỗi: {result.Error}"
            : result.JobId is int id
                ? $"Đã gửi tới spooler · Job #{id}"
                : "Đã gửi tới spooler.";
}
