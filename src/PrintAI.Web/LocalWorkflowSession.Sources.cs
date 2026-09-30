using PrintAI.Rendering;

namespace PrintAI.Web;

public sealed partial class LocalWorkflowSession
{
    public byte[] RenderSourceThumbnail(Guid sourceId)
    {
        UploadedSource source;
        lock (_sync)
        {
            source = _sources.TryGetValue(sourceId, out var found)
                ? found
                : throw new LocalWorkflowException("File không còn trong phiên hiện tại.");
        }

        return SourceJobRenderer.RenderSourceThumbnailPng(source.Path, 0, maxDimension: 320);
    }

    public byte[] RenderSourcePreview(Guid sourceId, int pageIndex)
    {
        UploadedSource source;
        lock (_sync)
        {
            source = _sources.TryGetValue(sourceId, out var found)
                ? found
                : throw new LocalWorkflowException("File không còn trong phiên hiện tại.");
        }

        if (pageIndex < 0 || pageIndex >= source.View.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Trang preview không hợp lệ.");

        return SourceJobRenderer.RenderSourceThumbnailPng(source.Path, pageIndex, maxDimension: 2048);
    }

    public RemoveLocalSourcesResult RemoveSources(IReadOnlyList<Guid> sourceIds)
    {
        if (sourceIds is null || sourceIds.Count is < 1 or > MaxFiles ||
            sourceIds.Distinct().Count() != sourceIds.Count)
        {
            throw new LocalWorkflowException("Chọn ít nhất một file hợp lệ để xóa.");
        }

        UploadedSource[] removedSources;
        LocalPrintJob[] removedJobs;
        lock (_sync)
        {
            removedSources = sourceIds.Select(id => _sources.TryGetValue(id, out var source)
                ? source
                : throw new LocalWorkflowException("Một file đã chọn không còn trong phiên.")).ToArray();
            var removedIds = sourceIds.ToHashSet();
            removedJobs = _jobs.Values
                .Where(job => job.SourceIds.Any(removedIds.Contains))
                .ToArray();
            var removedBytes = removedSources
                .Where(source => File.Exists(source.Path))
                .Sum(source => new FileInfo(source.Path).Length);

            foreach (var source in removedSources)
                _sources.Remove(source.View.Id);
            _storedBytes = Math.Max(0, _storedBytes - removedBytes);
            foreach (var job in removedJobs)
                _jobs.Remove(job.Id);
        }

        foreach (var source in removedSources)
            TryDelete(source.Path);
        foreach (var job in removedJobs)
            TryDeleteDirectory(Path.Combine(_workspace, "exports", job.Id.ToString("N")));

        return new(removedSources.Length, removedJobs.Select(job => job.Id).ToArray());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
