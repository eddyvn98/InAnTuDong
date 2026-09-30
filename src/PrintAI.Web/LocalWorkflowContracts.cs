namespace PrintAI.Web;

public sealed record UploadedSourceView(
    Guid Id,
    string FileName,
    string Kind,
    int PageCount,
    int? PixelWidth,
    int? PixelHeight);

public sealed record RemoveLocalSourcesRequest(IReadOnlyList<Guid> SourceIds);

public sealed record RemoveLocalSourcesResult(int RemovedCount, IReadOnlyList<Guid> RemovedJobIds);

public sealed record CreateLocalJobRequest(
    IReadOnlyList<Guid> SourceIds,
    double ItemWidthMm = 40,
    double ItemHeightMm = 60,
    int Copies = 1,
    double GapMm = 3,
    double MarginMm = 5);

public sealed record LocalJobView(
    Guid Id,
    int OutputPageCount,
    int ItemCount,
    int Columns,
    int Rows,
    int CapacityPerPage,
    bool Rotated);

public sealed record PrinterView(string Name, string Status, bool IsDefault);

public sealed record PrinterListView(bool Supported, IReadOnlyList<PrinterView> Printers, string? Message);

public sealed record PrintLocalJobRequest(string PrinterName, int Copies = 1);

public sealed record PrintSubmissionView(string PrinterName, int Copies, string Message);

public sealed class LocalWorkflowException(string message) : Exception(message);
