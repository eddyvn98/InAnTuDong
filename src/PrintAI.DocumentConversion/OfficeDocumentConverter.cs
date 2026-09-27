using System.Diagnostics;

namespace PrintAI.DocumentConversion;

public static class OfficeDocumentConverter
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".ppt",
            ".pptx"
        };

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path));

    public static string GetOutputPdfPath(
        string inputPath,
        string outputDirectory) =>
        Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

    public static string ConvertToPdf(
        string inputPath,
        string outputDirectory,
        string? executablePath = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Office source file does not exist.", inputPath);

        if (!IsSupported(inputPath))
        {
            throw new NotSupportedException(
                "Only DOC, DOCX, XLS, XLSX, PPT and PPTX are supported for Office conversion.");
        }

        Directory.CreateDirectory(outputDirectory);

        var executable = executablePath ?? FindLibreOfficeExecutable()
            ?? throw new InvalidOperationException(
                "Không tìm thấy LibreOffice/soffice. Cài LibreOffice hoặc đặt PRINTAI_LIBREOFFICE_PATH.");

        var expectedOutput = GetOutputPdfPath(inputPath, outputDirectory);
        if (File.Exists(expectedOutput))
            File.Delete(expectedOutput);

        var profileDirectory = Path.Combine(
            Path.GetTempPath(),
            $"printai-lo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);

        try
        {
            using var process = new Process
            {
                StartInfo = BuildStartInfo(
                    executable,
                    inputPath,
                    outputDirectory,
                    profileDirectory)
            };

            if (!process.Start())
                throw new InvalidOperationException("Không khởi động được LibreOffice converter.");

            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);
            using var cts = new CancellationTokenSource(effectiveTimeout);

            try
            {
                process.WaitForExitAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw new TimeoutException(
                    $"LibreOffice conversion exceeded {effectiveTimeout.TotalSeconds:0} seconds.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"LibreOffice conversion failed ({process.ExitCode}): {stderr.Trim()}");
            }

            if (!File.Exists(expectedOutput))
            {
                throw new InvalidOperationException(
                    $"LibreOffice finished but PDF was not created. {stdout.Trim()} {stderr.Trim()}".Trim());
            }

            return expectedOutput;
        }
        finally
        {
            try
            {
                Directory.Delete(profileDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    public static string? FindLibreOfficeExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("PRINTAI_LIBREOFFICE_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        foreach (var candidate in CommonWindowsPaths())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return FindOnPath(
            OperatingSystem.IsWindows() ? "soffice.exe" : "soffice");
    }

    private static ProcessStartInfo BuildStartInfo(
        string executable,
        string inputPath,
        string outputDirectory,
        string profileDirectory)
    {
        var profileUri = new Uri(
            Path.GetFullPath(profileDirectory) + Path.DirectorySeparatorChar);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add($"-env:UserInstallation={profileUri.AbsoluteUri}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(Path.GetFullPath(outputDirectory));
        startInfo.ArgumentList.Add(Path.GetFullPath(inputPath));

        return startInfo;
    }

    private static IEnumerable<string> CommonWindowsPaths()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        var programFiles = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(
                programFiles,
                "LibreOffice",
                "program",
                "soffice.exe");
        }

        var programFilesX86 = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(
                programFilesX86,
                "LibreOffice",
                "program",
                "soffice.exe");
        }
    }

    private static string? FindOnPath(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            try
            {
                var candidate = Path.Combine(directory.Trim(), executableName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}
