namespace PrintAI.Windows.Printing;

public enum PrintSubmissionState
{
    Submitted,
    SubmittedUntracked,
    Failed
}

public sealed record PrintSubmissionResult(
    PrintSubmissionState State,
    string PrinterName,
    string DocumentName,
    int? JobId = null,
    string? Error = null);

public enum SpoolerJobState
{
    Unknown,
    Queued,
    Printing,
    Completed,
    Error,
    Deleted
}

public sealed record SpoolerJobSnapshot(
    string PrinterName,
    int JobId,
    string DocumentName,
    SpoolerJobState State,
    int Position,
    string? StatusText = null);
