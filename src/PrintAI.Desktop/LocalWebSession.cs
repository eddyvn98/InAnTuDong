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

    private Task ExecuteCoreAsync(
        LocalWebAction request,
        CancellationToken cancellationToken) =>
        LocalWebActionDispatcher.ExecuteAsync(
            _desktop,
            request,
            cancellationToken);

}
