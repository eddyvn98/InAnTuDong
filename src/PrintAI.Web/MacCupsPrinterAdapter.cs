using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PrintAI.Web;

public sealed class MacCupsPrinterAdapter
{
    private static readonly Regex PrinterLine = new(
        "^printer ([^ ]+)\\s+(.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex DefaultLine = new(
        "^system default destination:\\s*(.+?)\\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SafeDestination = new(
        "^[\\p{L}\\p{N}_.-]{1,127}$", RegexOptions.CultureInvariant);
    private readonly SemaphoreSlim _submitGate = new(1, 1);

    public async Task<PrinterListView> ListAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
            return new(false, [], "Gửi lệnh in trực tiếp hiện chỉ hỗ trợ trên macOS.");

        var result = await RunAsync("/usr/bin/lpstat", ["-p", "-d"], cancellationToken);
        if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output))
            return new(false, [], "Không đọc được hàng đợi CUPS. Kiểm tra dịch vụ in của macOS.");

        var defaultMatch = DefaultLine.Match(result.Output);
        var defaultName = defaultMatch.Success ? defaultMatch.Groups[1].Value.Trim() : null;
        var printers = PrinterLine.Matches(result.Output)
            .Select(match => new PrinterView(match.Groups[1].Value, match.Groups[2].Value.Trim(),
                string.Equals(match.Groups[1].Value, defaultName, StringComparison.Ordinal)))
            .ToArray();
        var message = printers.Length == 0
            ? "Chưa có máy in được thêm vào macOS. Thêm máy in trong Cài đặt hệ thống → Máy in & Máy quét."
            : null;
        return new(true, printers, message);
    }

    public async Task<PrintSubmissionView> SubmitAsync(
        string printerName, int copies, string pdfPath, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
            throw new LocalWorkflowException("Gửi lệnh in trực tiếp hiện chỉ hỗ trợ trên macOS.");
        if (!SafeDestination.IsMatch(printerName))
            throw new LocalWorkflowException("Tên máy in không hợp lệ. Hãy tải lại danh sách máy in.");
        if (copies is < 1 or > 100)
            throw new LocalWorkflowException("Số bản gửi máy in phải từ 1 đến 100.");
        if (!File.Exists(pdfPath))
            throw new LocalWorkflowException("Không tìm thấy PDF của job. Hãy tạo preview lại.");

        await _submitGate.WaitAsync(cancellationToken);
        try
        {
            var available = await ListAsync(cancellationToken);
            if (!available.Supported || !available.Printers.Any(printer =>
                    string.Equals(printer.Name, printerName, StringComparison.Ordinal)))
            {
                throw new LocalWorkflowException("Máy in không còn khả dụng. Tải lại danh sách máy in.");
            }

            var title = $"PrintAI-{Guid.NewGuid():N}";
            var result = await RunAsync("/usr/bin/lp",
                ["-d", printerName, "-n", copies.ToString(), "-t", title, "--", pdfPath],
                cancellationToken);
            if (result.ExitCode != 0)
                throw new LocalWorkflowException(FormatPrintError(result.Error));

            return new(printerName, copies, string.IsNullOrWhiteSpace(result.Output)
                ? "Đã gửi PDF vào hàng đợi in của macOS."
                : result.Output.Trim());
        }
        finally
        {
            _submitGate.Release();
        }
    }

    private static async Task<CommandResult> RunAsync(
        string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                throw new LocalWorkflowException("Không thể khởi động tiện ích in của macOS.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                if (cancellationToken.IsCancellationRequested)
                    throw;
                throw new LocalWorkflowException("Dịch vụ in macOS phản hồi quá lâu. Hãy thử lại.");
            }
            return new(process.ExitCode, await outputTask, await errorTask);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new LocalWorkflowException("Không tìm thấy tiện ích CUPS lp/lpstat trên máy Mac.");
        }
    }

    private static string FormatPrintError(string error)
    {
        var detail = error.Trim();
        return string.IsNullOrEmpty(detail)
            ? "macOS không nhận lệnh in. Kiểm tra máy in và thử lại."
            : $"macOS không nhận lệnh in: {detail}";
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
