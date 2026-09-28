using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PrintAI.Domain;
using PrintAI.History;
using PrintAI.Rendering;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    public void ContinueManualDuplex()
    {
        var pending = _pendingManualDuplex;
        if (pending is null)
        {
            _status = "Không có job 2 mặt nào đang chờ in mặt sau.";
            return;
        }

        if (!string.Equals(
                _selectedPrinter,
                pending.PrinterName,
                StringComparison.OrdinalIgnoreCase))
        {
            _status =
                $"Job mặt sau thuộc máy in {pending.PrinterName}. " +
                "Hãy chọn đúng máy in trước khi tiếp tục.";
            return;
        }

        var profile = PrinterProfileCatalog.Resolve(pending.PrinterName);
        if (!string.Equals(
                profile.Id,
                pending.PrinterProfileId,
                StringComparison.Ordinal))
        {
            _status =
                "Printer profile đã thay đổi từ sau front pass. " +
                "Không thể tiếp tục mặt sau an toàn.";
            return;
        }

        var pages = pending.BackPass
            .Select(side => new PrintablePage(
                side.PngPath,
                side.RotationDegrees))
            .ToArray();

        var result = WindowsSpoolerPrinter.SubmitPages(
            pending.PrinterName,
            pages,
            pending.PaperWidthMm,
            pending.PaperHeightMm,
            pending.Landscape,
            profile,
            copies: 1,
            duplex: DuplexMode.Off);

        if (result.State == PrintSubmissionState.Failed)
        {
            _status =
                $"In mặt sau lỗi: {result.Error}. " +
                "Front pass đã hoàn tất; có thể retry mặt sau.";
            RecordPendingDuplex("manual-duplex-back", result, pending);
            return;
        }

        _status =
            $"Đã gửi back pass gồm {pages.Length} mặt sau tới spooler.";

        RecordPendingDuplex("manual-duplex-back", result, pending);
        ClearPendingManualDuplex(deleteArtifacts: true);
    }

    public void CancelManualDuplex()
    {
        if (_pendingManualDuplex is null)
        {
            _status = "Không có job manual duplex đang chờ.";
            return;
        }

        ClearPendingManualDuplex(deleteArtifacts: true);
        _status =
            "Đã hủy pending manual-duplex job. " +
            "Các mặt trước đã in sẽ không được tự động in lại.";
    }


    public void SaveManualDuplexProfile(
        string backOrder,
        int longEdgeRotationDegrees,
        int shortEdgeRotationDegrees,
        string reinsertInstruction,
        bool verified)
    {
        if (string.IsNullOrWhiteSpace(_selectedPrinter))
            throw new InvalidOperationException("Chưa chọn máy in.");

        if (!Enum.TryParse<ManualDuplexBackOrder>(
                backOrder,
                ignoreCase: true,
                out var parsedOrder))
        {
            throw new ArgumentException("Back order không hợp lệ.");
        }

        if (longEdgeRotationDegrees is not (0 or 180) ||
            shortEdgeRotationDegrees is not (0 or 180))
        {
            throw new ArgumentException(
                "Rotation manual duplex chỉ hỗ trợ 0 hoặc 180 độ.");
        }

        if (string.IsNullOrWhiteSpace(reinsertInstruction))
        {
            throw new ArgumentException(
                "Cần có hướng dẫn nạp lại giấy cho printer profile.");
        }

        var stored = new StoredManualDuplexProfile(
            PrinterName: _selectedPrinter,
            BackOrder: parsedOrder,
            LongEdgeBackRotationDegrees: longEdgeRotationDegrees,
            ShortEdgeBackRotationDegrees: shortEdgeRotationDegrees,
            ReinsertInstruction: reinsertInstruction.Trim(),
            IsVerified: verified,
            UpdatedAt: DateTimeOffset.Now);

        _manualDuplexCalibrationStore.Save(stored);

        _status = verified
            ? "Đã lưu manual-duplex profile và đánh dấu verified theo xác nhận test giấy thật."
            : "Đã lưu manual-duplex profile ở trạng thái chưa verified.";
    }

    private void PrintDuplexJob(
        DesktopPage page,
        PrintJobSpec job)
    {
        if (_pendingManualDuplex is not null)
        {
            _status =
                "Đang có một manual-duplex job chờ in mặt sau. " +
                "Hoàn tất hoặc hủy job đó trước khi bắt đầu job 2 mặt mới.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedPrinter))
        {
            _status = "Không tìm thấy máy in.";
            return;
        }

        var pageCount = SourceJobRenderer.GetOutputPageCount(job);
        var id = Guid.NewGuid().ToString("N");
        var artifactDirectory = Path.Combine(
            _workDir,
            "duplex",
            id);

        Directory.CreateDirectory(artifactDirectory);

        try
        {
            var outputPaths = RenderDuplexOutputPages(
                page,
                job,
                pageCount,
                artifactDirectory);

            var capability =
                PrinterCapabilityProbe.Inspect(_selectedPrinter);
            var profile =
                PrinterProfileCatalog.Resolve(_selectedPrinter);

            if (capability.CanDuplex)
            {
                PrintAutomaticDuplex(
                    job,
                    outputPaths,
                    profile,
                    artifactDirectory);
                return;
            }

            PrintManualDuplexFront(
                job,
                outputPaths,
                profile,
                pageCount,
                id,
                artifactDirectory);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(artifactDirectory);
            _status = $"Không thể chuẩn bị job 2 mặt: {ex.Message}";
        }
    }

    private void PrintAutomaticDuplex(
        PrintJobSpec job,
        IReadOnlyList<string> outputPaths,
        PrinterDeviceProfile profile,
        string artifactDirectory)
    {
        if (job.Print.Copies > short.MaxValue)
        {
            _status = "Số copy vượt giới hạn Windows printer driver.";
            TryDeleteDirectory(artifactDirectory);
            return;
        }

        var pages = outputPaths
            .Select(path => new PrintablePage(path))
            .ToArray();

        var result = WindowsSpoolerPrinter.SubmitPages(
            _selectedPrinter!,
            pages,
            job.Paper.WidthMm,
            job.Paper.HeightMm,
            job.Paper.Orientation == PageOrientation.Landscape,
            profile,
            copies: (short)job.Print.Copies,
            duplex: job.Print.Duplex);

        _status = result.State == PrintSubmissionState.Failed
            ? $"In 2 mặt tự động lỗi: {result.Error}"
            : $"Đã gửi {pages.Length} output page theo chế độ {job.Print.Duplex}.";

        RecordPrint("print-job-duplex-auto", result, job);
        TryDeleteDirectory(artifactDirectory);
    }

    private void PrintManualDuplexFront(
        PrintJobSpec job,
        IReadOnlyList<string> outputPaths,
        PrinterDeviceProfile profile,
        int pageCount,
        string id,
        string artifactDirectory)
    {
        var manualProfile =
            ResolveManualDuplexProfile(profile);

        var plan = ManualDuplexPlanner.Create(
            pageCount,
            job.Print.Copies,
            job.Print.Duplex,
            manualProfile.BackOrder,
            manualProfile.LongEdgeBackRotationDegrees,
            manualProfile.ShortEdgeBackRotationDegrees);

        var frontPages = plan.FrontPass
            .Select(side => new PrintablePage(
                outputPaths[side.OutputPageIndex],
                side.RotationDegrees))
            .ToArray();

        var result = WindowsSpoolerPrinter.SubmitPages(
            _selectedPrinter!,
            frontPages,
            job.Paper.WidthMm,
            job.Paper.HeightMm,
            job.Paper.Orientation == PageOrientation.Landscape,
            profile,
            copies: 1,
            duplex: DuplexMode.Off);

        if (result.State == PrintSubmissionState.Failed)
        {
            _status = $"Front pass lỗi: {result.Error}";
            RecordPrint("manual-duplex-front", result, job);
            TryDeleteDirectory(artifactDirectory);
            return;
        }

        RecordPrint("manual-duplex-front", result, job);

        if (plan.BackPass.Count == 0)
        {
            _status =
                "Job chỉ có một mặt vật lý; front pass đã hoàn tất và không có back pass.";
            TryDeleteDirectory(artifactDirectory);
            return;
        }

        var pending = new PendingManualDuplexJob(
            Id: id,
            JobName: job.JobName,
            PrinterName: _selectedPrinter!,
            PrinterProfileId: profile.Id,
            Mode: job.Print.Duplex,
            CreatedAt: DateTimeOffset.Now,
            SheetCount: plan.Sheets.Count,
            ProfileVerified: manualProfile.IsVerified,
            ReinsertInstruction: manualProfile.ReinsertInstruction,
            PaperWidthMm: job.Paper.WidthMm,
            PaperHeightMm: job.Paper.HeightMm,
            Landscape: job.Paper.Orientation == PageOrientation.Landscape,
            JobFingerprint: Fingerprint(job),
            ArtifactDirectory: artifactDirectory,
            BackPass: plan.BackPass
                .Select(side => new PendingManualDuplexSide(
                    outputPaths[side.OutputPageIndex],
                    side.RotationDegrees))
                .ToArray());

        _manualDuplexStore.Save(pending);
        _pendingManualDuplex = pending;

        _status =
            $"Front pass hoàn tất: {plan.Sheets.Count} tờ. " +
            "Hãy nạp lại xấp giấy theo hướng dẫn rồi bấm in mặt sau.";
    }

    private IReadOnlyList<string> RenderDuplexOutputPages(
        DesktopPage page,
        PrintJobSpec job,
        int pageCount,
        string artifactDirectory)
    {
        var paths = new string[pageCount];

        for (var outputPage = 0;
             outputPage < pageCount;
             outputPage++)
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
                artifactDirectory,
                $"page-{outputPage:D4}.png");

            File.WriteAllBytes(path, png);
            paths[outputPage] = path;
        }

        return paths;
    }

    private DesktopDuplexView BuildDuplexView(
        PrintJobSpec? job)
    {
        if (_pendingManualDuplex is { } pending)
        {
            return new(
                Mode: pending.Mode.ToString(),
                Pending: true,
                SheetCount: pending.SheetCount,
                PrinterName: pending.PrinterName,
                ProfileVerified: pending.ProfileVerified,
                Instruction: pending.ReinsertInstruction,
                CanContinueBack:
                    string.Equals(
                        _selectedPrinter,
                        pending.PrinterName,
                        StringComparison.OrdinalIgnoreCase),
                BackOrder: "",
                LongEdgeBackRotationDegrees: 0,
                ShortEdgeBackRotationDegrees: 0);
        }

        var profile =
            string.IsNullOrWhiteSpace(_selectedPrinter)
                ? null
                : PrinterProfileCatalog.Resolve(_selectedPrinter);

        var manualProfile = profile is null
            ? ManualDuplexProfile.UnverifiedDefault
            : ResolveManualDuplexProfile(profile);

        return new(
            Mode: (job?.Print.Duplex ?? DuplexMode.Off).ToString(),
            Pending: false,
            SheetCount: 0,
            PrinterName: _selectedPrinter,
            ProfileVerified: manualProfile.IsVerified,
            Instruction: manualProfile.ReinsertInstruction,
            CanContinueBack: false,
            BackOrder: manualProfile.BackOrder.ToString(),
            LongEdgeBackRotationDegrees:
                manualProfile.LongEdgeBackRotationDegrees,
            ShortEdgeBackRotationDegrees:
                manualProfile.ShortEdgeBackRotationDegrees);
    }


    private ManualDuplexProfile ResolveManualDuplexProfile(
        PrinterDeviceProfile printerProfile)
    {
        if (!string.IsNullOrWhiteSpace(_selectedPrinter) &&
            _manualDuplexCalibrationStore.Get(_selectedPrinter) is
                { } stored)
        {
            return stored.ToProfile();
        }

        return printerProfile.ManualDuplex ??
               ManualDuplexProfile.UnverifiedDefault;
    }

    private void ClearPendingManualDuplex(
        bool deleteArtifacts)
    {
        var directory = _pendingManualDuplex?.ArtifactDirectory;

        _manualDuplexStore.Clear();
        _pendingManualDuplex = null;

        if (deleteArtifacts &&
            !string.IsNullOrWhiteSpace(directory))
        {
            TryDeleteDirectory(directory);
        }
    }

    private void RecordPendingDuplex(
        string action,
        PrintSubmissionResult result,
        PendingManualDuplexJob pending) =>
        _history.Append(new JobHistoryEntry(
            DateTimeOffset.Now,
            action,
            result.State.ToString(),
            _lastRequest,
            pending.PrinterName,
            pending.JobName,
            result.Error ?? result.JobId?.ToString()));

    private static string Fingerprint(
        PrintJobSpec job)
    {
        var json = JsonSerializer.Serialize(job);
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(json)));
    }

    private static void TryDeleteDirectory(
        string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Work artifacts are safe to clean on a later run.
        }
    }
}
