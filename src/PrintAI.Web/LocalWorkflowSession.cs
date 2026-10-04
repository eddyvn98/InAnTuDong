using System.Security.Cryptography;
using System.Text;
using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Planning;
using PrintAI.DocumentConversion;
using PrintAI.Rendering;
using PrintAI.Scanning;
using PrintAI.SourceInspection;

namespace PrintAI.Web;

public sealed partial class LocalWorkflowSession : IDisposable
{
    private const long MaxFileBytes = 100L * 1024 * 1024;
    private const long MaxSessionBytes = 256L * 1024 * 1024;
    private const int MaxFiles = 100;
    private const int MaxPdfPagesPerSource = 200;
    private const int MaxOutputPages = 20;
    private const int MaxItems = 1000;
    private static readonly HashSet<string> AllowedExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".heic", ".heif", ".pdf",
            ".doc", ".docx", ".docm", ".dot", ".dotx", ".dotm", ".rtf",
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".xlt", ".xltx", ".xltm",
            ".ppt", ".pptx", ".pptm", ".pps", ".ppsx", ".ppsm", ".pot", ".potx", ".potm"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "PrintAI", "mac-web", Guid.NewGuid().ToString("N"));
    private readonly object _sync = new();
    private readonly SemaphoreSlim _uploadGate = new(1, 1);
    private readonly Dictionary<Guid, UploadedSource> _sources = [];
    private readonly Dictionary<Guid, LocalPrintJob> _jobs = [];
    private long _storedBytes;

    public LocalWorkflowSession()
    {
        SessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Directory.CreateDirectory(_workspace);
    }

    public string SessionToken { get; }

    public IReadOnlyList<UploadedSourceView> ListSources()
    {
        lock (_sync)
            return _sources.Values.Select(source => source.View).ToArray();
    }

    public async Task<IReadOnlyList<UploadedSourceView>> AddFilesAsync(
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count is < 1 or > MaxFiles)
            throw new LocalWorkflowException("Chọn từ 1 đến 100 tệp.");

        var extensions = files.Select(file => Path.GetExtension(file.FileName)).ToArray();
        if (extensions.Any(extension => !AllowedExtensions.Contains(extension)))
            throw new LocalWorkflowException("Chỉ nhận ảnh, PDF và các định dạng Microsoft Office được hỗ trợ.");

        if (files.Any(file => file.Length <= 0 || file.Length > MaxFileBytes))
            throw new LocalWorkflowException("Mỗi tệp phải có dung lượng từ 1 byte đến 100 MB.");

        var requestBytes = files.Sum(file => file.Length);
        await _uploadGate.WaitAsync(cancellationToken);
        try
        {
            lock (_sync)
            {
                if (_sources.Count + files.Count > MaxFiles ||
                    _storedBytes + requestBytes > MaxSessionBytes)
                {
                    throw new LocalWorkflowException(
                        "Phiên local tối đa 100 tệp và 256 MB. Khởi động lại ứng dụng để dọn phiên hiện tại.");
                }
            }

            var added = new List<UploadedSource>(files.Count);
            var createdPaths = new List<string>(files.Count);
            try
            {
                for (var index = 0; index < files.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = files[index];
                    var id = Guid.NewGuid();
                    var path = Path.Combine(_workspace, $"{id:N}{extensions[index]}");
                    createdPaths.Add(path);

                    await using (var output = new FileStream(
                                     path,
                                     FileMode.CreateNew,
                                     FileAccess.Write,
                                     FileShare.None,
                                     81920,
                                     FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await file.CopyToAsync(output, cancellationToken);
                    }

                    var sourcePath = path;
                    if (OfficeDocumentConverter.IsSupported(path))
                    {
                        var convertedDirectory = Path.Combine(_workspace, "converted", id.ToString("N"));
                        sourcePath = OfficeDocumentConverter.ConvertToPdf(path, convertedDirectory);
                        createdPaths.Add(sourcePath);
                        TryDelete(path);
                    }

                    var metadata = SourceInspector.Inspect(sourcePath);
                    var pageCount = metadata.PageCount ?? 1;
                    if (pageCount is < 1 or > MaxPdfPagesPerSource)
                        throw new LocalWorkflowException("Mỗi PDF được hỗ trợ tối đa 200 trang.");

                    added.Add(new(
                        new UploadedSourceView(
                            id,
                            Path.GetFileName(file.FileName),
                            metadata.Kind.ToString(),
                            pageCount,
                            metadata.PixelWidth,
                            metadata.PixelHeight),
                        sourcePath));
                }

                lock (_sync)
                {
                    foreach (var source in added)
                        _sources.Add(source.View.Id, source);
                    _storedBytes += requestBytes;
                }

                return added.Select(source => source.View).ToArray();
            }
            catch
            {
                foreach (var path in createdPaths)
                    TryDelete(path);
                throw;
            }
        }
        finally
        {
            _uploadGate.Release();
        }
    }

    public async Task<Guid> CreateTextSourceAsync(
        string userRequest,
        LocalWorkflowPlanner planner,
        CancellationToken cancellationToken,
        IProgress<PlannerProgressUpdate>? progress = null)
    {
        var content = await planner.CreateSourceAsync(userRequest, cancellationToken, progress);
        if (content.StartsWith("[NEEDS_TOOL:", StringComparison.OrdinalIgnoreCase))
            throw new LocalWorkflowException(content);

        var id = Guid.NewGuid();
        var path = Path.Combine(_workspace, $"{id:N}.pdf");
        var pageImagePath = Path.Combine(_workspace, $"{id:N}.png");
        try
        {
            File.WriteAllBytes(
                pageImagePath,
                PrintableTextRenderer.RenderA4Png(content, dpi: 180));
            ScanPdfWriter.Write([pageImagePath], path);
        }
        finally
        {
            TryDelete(pageImagePath);
        }

        var metadata = SourceInspector.Inspect(path);
        var view = new UploadedSourceView(
            id,
            "AI-generated.pdf",
            metadata.Kind.ToString(),
            metadata.PageCount ?? 1,
            metadata.PixelWidth,
            metadata.PixelHeight);
        lock (_sync)
        {
            _sources.Add(id, new UploadedSource(view, path));
            _storedBytes += new FileInfo(path).Length;
        }
        return id;
    }

    public LocalJobView CreateJob(CreateLocalJobRequest request)
    {
        if (request.SourceIds is null ||
            request.SourceIds.Count is < 1 or > MaxFiles ||
            request.SourceIds.Distinct().Count() != request.SourceIds.Count)
        {
            throw new LocalWorkflowException("Chọn ít nhất một tệp đã tải lên.");
        }

        if (!double.IsFinite(request.ItemWidthMm) || request.ItemWidthMm is < 5 or > 200 ||
            !double.IsFinite(request.ItemHeightMm) || request.ItemHeightMm is < 5 or > 287 ||
            request.Copies is < 1 or > 100 ||
            !double.IsFinite(request.GapMm) || request.GapMm is < 0 or > 50 ||
            !double.IsFinite(request.MarginMm) || request.MarginMm is < 0 or > 50)
        {
            throw new LocalWorkflowException("Kiểm tra lại kích thước, số bản, khe và lề.");
        }

        UploadedSource[] selected;
        lock (_sync)
        {
            selected = request.SourceIds
                .Select(id => _sources.TryGetValue(id, out var source)
                    ? source
                    : throw new LocalWorkflowException("Một tệp đã chọn không còn trong phiên."))
                .ToArray();
        }

        var pageTotal = selected.Sum(source => source.View.PageCount);
        if (pageTotal * request.Copies > MaxItems)
            throw new LocalWorkflowException("Một job được hỗ trợ tối đa 1.000 bản/trang nguồn.");

        var sourceSpecs = selected.SelectMany(source =>
            Enumerable.Range(0, source.View.PageCount).Select(page =>
                new SourceSpec(source.Path, request.Copies, PageIndex: page))).ToArray();

        var spec = new PrintJobSpec(
            "Mac local web job",
            sourceSpecs,
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid,
                request.ItemWidthMm,
                request.ItemHeightMm,
                request.GapMm,
                request.MarginMm,
                AllowRotate: true,
                CutMarks: true,
                Fit: FitMode.Contain),
            new PrintSettings(Quality: PrintQuality.High),
            new PolicySpec());

        var layout = LayoutEngine.Layout(spec);
        var outputPageCount = layout.Placements.Count == 0
            ? 0
            : layout.Placements.Max(item => item.Page) + 1;
        if (layout.Placements.Count > MaxItems || outputPageCount > MaxOutputPages)
            throw new LocalWorkflowException("Job vượt giới hạn 1.000 mục hoặc 20 trang A4.");

        var id = Guid.NewGuid();
        var job = new LocalPrintJob(
            id,
            [new LocalPrintBatch(spec, layout, outputPageCount)],
            outputPageCount,
            request.SourceIds.ToArray());
        lock (_sync)
        {
            if (_jobs.Count >= 20)
                throw new LocalWorkflowException("Phiên hiện tại tối đa 20 job. Khởi động lại ứng dụng để dọn phiên.");
            _jobs.Add(id, job);
        }

        return new(
            id,
            outputPageCount,
            layout.Placements.Count,
            layout.Columns,
            layout.Rows,
            layout.CapacityPerPage,
            layout.Rotated);
    }

    public byte[] RenderPreview(Guid jobId, int page)
    {
        var job = GetJob(jobId);
        if (page < 0 || page >= job.OutputPageCount)
            throw new LocalWorkflowException("Trang preview không tồn tại.");

        var target = ResolveBatchPage(job, page);
        return SourceJobRenderer.RenderMixedA4(target.Batch.Spec, target.Page, dpi: 120);
    }

    public string CreatePdf(Guid jobId)
    {
        lock (_sync)
        {
            var job = GetJob(jobId);
            var exportDirectory = Path.Combine(_workspace, "exports", jobId.ToString("N"));
            Directory.CreateDirectory(exportDirectory);

            var pagePaths = new List<string>(job.OutputPageCount);
            for (var page = 0; page < job.OutputPageCount; page++)
            {
                var imagePath = Path.Combine(exportDirectory, $"page-{page + 1:D3}.png");
                var target = ResolveBatchPage(job, page);
                File.WriteAllBytes(imagePath, SourceJobRenderer.RenderMixedA4(target.Batch.Spec, target.Page, dpi: 300));
                pagePaths.Add(imagePath);
            }

            var pdfPath = Path.Combine(exportDirectory, "PrintAI-A4.pdf");
            ScanPdfWriter.Write(pagePaths, pdfPath);
            return pdfPath;
        }
    }

    private static (LocalPrintBatch Batch, int Page) ResolveBatchPage(LocalPrintJob job, int page)
    {
        var offset = 0;
        foreach (var batch in job.Batches)
        {
            if (page < offset + batch.OutputPageCount)
                return (batch, page - offset);
            offset += batch.OutputPageCount;
        }
        throw new LocalWorkflowException("Trang preview không tồn tại.");
    }

    private LocalPrintJob GetJob(Guid id)
    {
        lock (_sync)
        {
            return _jobs.TryGetValue(id, out var job)
                ? job
                : throw new LocalWorkflowException("Job không còn trong phiên hiện tại.");
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspace))
                Directory.Delete(_workspace, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record UploadedSource(UploadedSourceView View, string Path);
    private sealed record LocalPrintBatch(
        PrintJobSpec Spec,
        LayoutResult Layout,
        int OutputPageCount);

    private sealed record LocalPrintJob(
        Guid Id,
        IReadOnlyList<LocalPrintBatch> Batches,
        int OutputPageCount,
        IReadOnlyList<Guid> SourceIds);
}
