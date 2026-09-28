using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.AspNetCore.Http;
using PrintAI.Domain;

namespace PrintAI.Desktop;

public sealed record LocalWebAction(
    string Action,
    JsonElement Payload);

public sealed class LocalWebSession : IDisposable
{
    private const long MaxFileBytes = 100L * 1024 * 1024;
    private const int MaxFiles = 100;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".heic", ".heif", ".pdf",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
        };

    private readonly DesktopSession _desktop;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _uploadRoot;

    public LocalWebSession(
        DesktopSession desktop,
        Dispatcher dispatcher)
    {
        _desktop = desktop;
        _dispatcher = dispatcher;

        Token = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();

        _uploadRoot = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PrintAI",
            "local-web",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_uploadRoot);
    }

    public string Token { get; }

    public async Task<DesktopState> ReadStateAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await _dispatcher.InvokeAsync(
                _desktop.BuildState).Task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopState> AddUploadedFilesAsync(
        IFormFileCollection files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            throw new ArgumentException("Chọn ít nhất một file.");

        if (files.Count > MaxFiles)
        {
            throw new ArgumentException(
                $"Tối đa {MaxFiles} file cho mỗi lần upload.");
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            var batchDirectory = Path.Combine(
                _uploadRoot,
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(batchDirectory);

            var saved = new List<string>(files.Count);

            foreach (var file in files)
            {
                if (file.Length <= 0)
                    continue;

                if (file.Length > MaxFileBytes)
                {
                    throw new ArgumentException(
                        $"{file.FileName}: file vượt quá 100 MB.");
                }

                var name = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(name);

                if (string.IsNullOrWhiteSpace(name) ||
                    !SupportedExtensions.Contains(extension))
                {
                    throw new ArgumentException(
                        $"{file.FileName}: định dạng chưa hỗ trợ.");
                }

                var destination = UniqueDestination(
                    batchDirectory,
                    name);

                await using var stream = File.Create(destination);
                await file.CopyToAsync(
                    stream,
                    cancellationToken);

                saved.Add(destination);
            }

            if (saved.Count == 0)
                throw new ArgumentException("Không có file hợp lệ để thêm.");

            await _dispatcher.InvokeAsync(
                () => _desktop.AddPaths(saved)).Task;

            return await _dispatcher.InvokeAsync(
                _desktop.BuildState).Task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopState> ExecuteAsync(
        LocalWebAction request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            return await _dispatcher.InvokeAsync(
                async () =>
                {
                    await ExecuteCoreAsync(
                        request,
                        cancellationToken);

                    return _desktop.BuildState();
                }).Task.Unwrap();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();

        try
        {
            if (Directory.Exists(_uploadRoot))
            {
                Directory.Delete(
                    _uploadRoot,
                    recursive: true);
            }
        }
        catch
        {
            // Temporary upload cleanup is best effort at app shutdown.
        }
    }

    private async Task ExecuteCoreAsync(
        LocalWebAction request,
        CancellationToken cancellationToken)
    {
        var payload = request.Payload;

        switch (request.Action)
        {
            case "configureAntigravity":
                _desktop.ConfigureAntigravity(
                    ReadOptionalString(payload, "cliPath"));
                break;

            case "plan":
                await _desktop.PlanAsync(
                    ReadString(payload, "request"),
                    ReadString(payload, "mode"),
                    cancellationToken);
                break;

            case "selectPrinter":
                _desktop.SelectPrinter(
                    ReadString(payload, "printer"));
                break;

            case "selectPage":
                _desktop.SelectPage(
                    ReadInt(payload, "index"));
                break;

            case "selectOutputPage":
                _desktop.SelectOutputPage(
                    ReadInt(payload, "index"));
                break;

            case "selectPlanBatch":
                _desktop.SelectPlanBatch(
                    ReadInt(payload, "index"));
                break;

            case "applyJobSettings":
                _desktop.ApplyJobEdits(
                    ReadEdits(payload));
                break;

            case "print":
                _desktop.PrintCurrent();
                break;

            case "printJob":
                _desktop.PrintJob();
                break;

            case "printPlan":
                _desktop.PrintPlan();
                break;

            case "printAllSources":
                _desktop.PrintAllSources();
                break;

            case "continueManualDuplex":
                _desktop.ContinueManualDuplex();
                break;

            case "cancelManualDuplex":
                _desktop.CancelManualDuplex();
                break;

            case "refreshReadiness":
                _desktop.RefreshReadiness();
                break;

            case "clearHistory":
                _desktop.ClearHistory();
                break;

            case "clear":
                _desktop.Clear();
                break;

            default:
                throw new NotSupportedException(
                    $"Local web action chưa hỗ trợ: {request.Action}");
        }
    }

    private static DesktopJobEdits ReadEdits(
        JsonElement payload)
    {
        if (!Enum.TryParse<LayoutMode>(
                ReadString(payload, "mode"),
                ignoreCase: true,
                out var mode))
        {
            throw new ArgumentException("Layout mode không hợp lệ.");
        }

        if (!Enum.TryParse<FitMode>(
                ReadString(payload, "fit"),
                ignoreCase: true,
                out var fit))
        {
            throw new ArgumentException("Fit mode không hợp lệ.");
        }

        if (!Enum.TryParse<DuplexMode>(
                ReadString(payload, "duplex"),
                ignoreCase: true,
                out var duplex))
        {
            throw new ArgumentException("Duplex mode không hợp lệ.");
        }

        return new(
            mode,
            ReadDouble(payload, "itemWidthMm"),
            ReadDouble(payload, "itemHeightMm"),
            ReadDouble(payload, "gapMm"),
            ReadDouble(payload, "marginMm"),
            ReadInt(payload, "copies"),
            duplex,
            ReadBool(payload, "allowRotate"),
            ReadBool(payload, "cutMarks"),
            fit);
    }

    private static string UniqueDestination(
        string directory,
        string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return path;

        return Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(fileName)}-" +
            $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
    }

    private static string ReadString(
        JsonElement payload,
        string name) =>
        payload.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException(
                $"Thiếu giá trị {name}.");

    private static string? ReadOptionalString(
        JsonElement payload,
        string name) =>
        payload.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int ReadInt(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name).GetInt32();

    private static double ReadDouble(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name).GetDouble();

    private static bool ReadBool(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name).GetBoolean();
}
