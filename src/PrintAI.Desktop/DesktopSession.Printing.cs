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
        if (_pendingManualDuplex is not null)
        {
            _status =
                "Có manual-duplex job đang chờ mặt sau. Hoàn tất hoặc hủy job đó trước khi in nội dung khác.";
            return;
        }

        if (_printPath is null)
        {
            _status = "Chưa có trang preview có thể in.";
            return;
        }

        var page = CurrentPage()
            ?? throw new InvalidOperationException("Không có trang đang chọn.");

        var job = CurrentJob(page);
        if (job.Print.Duplex != DuplexMode.Off)
        {
            _status =
                "Job đang yêu cầu in 2 mặt. Hãy dùng 'In toàn bộ job' để app quản lý đúng front/back pass.";
            return;
        }

        var result = Submit(_printPath, job);

        _status = Describe(result);
        RecordPrint("print-page", result, job);
    }

    public void PrintJob()
    {
        if (_pendingManualDuplex is not null)
        {
            _status =
                "Có manual-duplex job đang chờ mặt sau. Hoàn tất hoặc hủy job đó trước khi in nội dung khác.";
            return;
        }

        var page = CurrentPage();
        if (page is null)
        {
            _status = "Không có trang để in.";
            return;
        }

        var job = CurrentJob(page);

        if (job.Print.Duplex != DuplexMode.Off)
        {
            PrintDuplexJob(page, job);
            return;
        }

        PrintSimplexJob(job, "print-job");
    }

    public void PrintPlan()
    {
        if (_pendingManualDuplex is not null)
        {
            _status =
                "Có manual-duplex job đang chờ mặt sau. Hoàn tất hoặc hủy job đó trước khi in plan.";
            return;
        }

        if (_compiledPlan is null || _compiledPlan.Batches.Count == 0)
        {
            _status = "Không có PrintPlan nhiều batch để in.";
            return;
        }

        if (_compiledPlan.Batches.Any(batch =>
                batch.Job.Print.Duplex != DuplexMode.Off))
        {
            _status =
                "PrintPlan có batch duplex. Để tránh sai thứ tự/xấp giấy, hãy chọn và in từng batch theo danh sách.";
            return;
        }

        for (var index = 0; index < _compiledPlan.Batches.Count; index++)
        {
            var batch = _compiledPlan.Batches[index];
            _selectedPlanBatch = index;
            _activeJob = batch.Job;

            if (!PrintSimplexJob(batch.Job, "print-plan-batch"))
            {
                ActivatePlanBatch(index, rebuildPreview: true);
                _status =
                    $"PrintPlan dừng ở batch {index + 1}/{_compiledPlan.Batches.Count}: " +
                    _status;
                return;
            }
        }

        ActivatePlanBatch(
            _compiledPlan.Batches.Count - 1,
            rebuildPreview: true);

        _status =
            $"Đã gửi toàn bộ {_compiledPlan.Batches.Count} batch simplex của PrintPlan tới spooler.";
    }

    public void PrintAllSources()
    {
        if (_pendingManualDuplex is not null)
        {
            _status =
                "Có manual-duplex job đang chờ mặt sau. Hoàn tất hoặc hủy job đó trước khi in nội dung khác.";
            return;
        }

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

                var result = Submit(path, job);
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

    private bool PrintSimplexJob(
        PrintJobSpec job,
        string historyAction)
    {
        if (job.Print.Duplex != DuplexMode.Off)
            throw new ArgumentException("PrintSimplexJob only accepts one-sided jobs.", nameof(job));

        var pageCount = SourceJobRenderer.GetOutputPageCount(job);
        var submitted = 0;
        var renderId = Guid.NewGuid().ToString("N");

        for (var outputPage = 0;
             outputPage < pageCount;
             outputPage++)
        {
            try
            {
                var primarySource = job.Sources[0];
                var png = RequiresMixedRenderer(job)
                    ? SourceJobRenderer.RenderMixedA4(
                        job,
                        outputPage,
                        dpi: 300)
                    : SourceJobRenderer.RenderA4(
                        job,
                        primarySource.Path,
                        primarySource.PageIndex,
                        outputPage,
                        dpi: 300);

                var path = Path.Combine(
                    _workDir,
                    $"{historyAction}-{renderId}-{outputPage:D4}.png");

                File.WriteAllBytes(path, png);

                var result = Submit(path, job);
                if (result.State == PrintSubmissionState.Failed)
                {
                    _status =
                        $"Dừng ở output {outputPage + 1}/{pageCount}: " +
                        result.Error;

                    RecordPrint(historyAction, result, job);
                    return false;
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
                    historyAction,
                    "Failed",
                    _lastRequest,
                    _selectedPrinter,
                    job.JobName,
                    ex.Message));

                return false;
            }
        }

        _status =
            $"Đã gửi {submitted}/{pageCount} output page tới spooler.";

        _history.Append(new(
            DateTimeOffset.Now,
            historyAction,
            "Submitted",
            _lastRequest,
            _selectedPrinter,
            job.JobName,
            _status));

        return true;
    }

    private PrintSubmissionResult Submit(
        string path,
        PrintJobSpec job)
    {
        if (job.Print.Duplex != DuplexMode.Off)
        {
            return new(
                PrintSubmissionState.Failed,
                _selectedPrinter ?? "",
                "",
                Error:
                    "Duplex intent cannot be submitted as a one-page simplex job. Use PrintJob().");
        }

        if (string.IsNullOrWhiteSpace(_selectedPrinter))
        {
            return new(
                PrintSubmissionState.Failed,
                "",
                "",
                Error: "Không tìm thấy máy in.");
        }

        var profile = PrinterProfileCatalog.Resolve(_selectedPrinter);

        return WindowsSpoolerPrinter.SubmitPng(
            _selectedPrinter,
            path,
            job.Paper.WidthMm,
            job.Paper.HeightMm,
            job.Paper.Orientation == PageOrientation.Landscape,
            profile,
            colorMode: job.Print.ColorMode);
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
