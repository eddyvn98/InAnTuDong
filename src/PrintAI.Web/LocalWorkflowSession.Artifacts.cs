using PrintAI.DocumentConversion;
using PrintAI.Planning;
using PrintAI.SourceInspection;

namespace PrintAI.Web;

public sealed partial class LocalWorkflowSession
{
    public async Task<LocalArtifactResult> ExecuteArtifactTaskAsync(
        IReadOnlyList<Guid> sourceIds,
        string userRequest,
        LocalWorkflowPlanner planner,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null)
    {
        UploadedSource[] selected;
        lock (_sync)
        {
            selected = sourceIds.Select(id =>
                _sources.TryGetValue(id, out var source)
                    ? source
                    : throw new LocalWorkflowException("Một tệp đã chọn không còn trong phiên.")).ToArray();
        }

        var taskId = Guid.NewGuid();
        var taskRoot = Path.Combine(_workspace, "artifact-tasks", taskId.ToString("N"));
        var inputDir = Path.Combine(taskRoot, "input");
        var outputDir = Path.Combine(taskRoot, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        var manifest = new List<string>();
        for (var index = 0; index < selected.Length; index++)
        {
            var source = selected[index];
            var extension = Path.GetExtension(source.OriginalPath);
            var safeName = $"{index + 1:D2}-{SanitizeFileName(source.View.FileName, extension)}";
            var target = Path.Combine(inputDir, safeName);
            File.Copy(source.OriginalPath, target, overwrite: false);
            manifest.Add($"input/{safeName} | original name: {source.View.FileName} | kind: {source.View.Kind}");
        }

        var summary = await planner.ExecuteArtifactAsync(
            userRequest,
            taskRoot,
            manifest.Count == 0 ? "(no input files; create the requested artifact)" : string.Join("\n", manifest),
            cancellationToken,
            progress);

        var outputFiles = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
            .ToArray();

        if (outputFiles.Length == 0)
        {
            throw new LocalWorkflowException(
                "AGY đã xử lý nhưng chưa tạo file kết quả trong output/. Không có gì để đưa vào preview.");
        }

        if (outputFiles.Length > MaxFiles)
            throw new LocalWorkflowException("AGY tạo quá nhiều file kết quả cho một yêu cầu.");

        var totalBytes = outputFiles.Sum(path => new FileInfo(path).Length);
        if (outputFiles.Any(path => new FileInfo(path).Length is <= 0 or > MaxFileBytes))
            throw new LocalWorkflowException("Một file kết quả vượt giới hạn 100 MB.");

        lock (_sync)
        {
            if (_sources.Count + outputFiles.Length > MaxFiles || _storedBytes + totalBytes > MaxSessionBytes)
                throw new LocalWorkflowException("File kết quả vượt giới hạn 100 file hoặc 256 MB của phiên.");
        }

        var added = new List<UploadedSourceView>();
        foreach (var output in outputFiles)
            added.Add(RegisterArtifactOutput(output));

        return new(added, summary);
    }

    private UploadedSourceView RegisterArtifactOutput(string outputPath)
    {
        var id = Guid.NewGuid();
        var extension = Path.GetExtension(outputPath);
        if (!AllowedExtensions.Contains(extension))
            throw new LocalWorkflowException("AGY tạo định dạng file chưa được PrintAI hỗ trợ.");

        var originalPath = Path.Combine(_workspace, $"{id:N}{extension}");
        File.Copy(outputPath, originalPath, overwrite: false);

        var previewPath = originalPath;
        if (OfficeDocumentConverter.IsSupported(originalPath))
        {
            var convertedDirectory = Path.Combine(_workspace, "converted", id.ToString("N"));
            previewPath = OfficeDocumentConverter.ConvertToPdf(originalPath, convertedDirectory);
        }

        var metadata = SourceInspector.Inspect(previewPath);
        var pageCount = metadata.PageCount ?? 1;
        if (pageCount is < 1 or > MaxPdfPagesPerSource)
        {
            TryDelete(originalPath);
            if (!string.Equals(previewPath, originalPath, StringComparison.OrdinalIgnoreCase))
                TryDelete(previewPath);
            throw new LocalWorkflowException("File kết quả có quá nhiều trang để preview.");
        }

        var view = new UploadedSourceView(
            id,
            Path.GetFileName(outputPath),
            metadata.Kind.ToString(),
            pageCount,
            metadata.PixelWidth,
            metadata.PixelHeight);

        lock (_sync)
        {
            _sources.Add(id, new UploadedSource(view, previewPath, originalPath));
            _storedBytes += new FileInfo(originalPath).Length;
        }

        return view;
    }

    private static string SanitizeFileName(string fileName, string fallbackExtension)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
            name += fallbackExtension;

        return string.IsNullOrWhiteSpace(name) ? $"source{fallbackExtension}" : name;
    }
}

public sealed record LocalArtifactResult(
    IReadOnlyList<UploadedSourceView> Sources,
    string Summary);
