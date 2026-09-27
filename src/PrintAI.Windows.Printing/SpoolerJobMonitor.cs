using System.Printing;

namespace PrintAI.Windows.Printing;

public static class SpoolerJobMonitor
{
    public static SpoolerJobSnapshot? FindByDocumentName(
        string printerName,
        string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        using var queue = OpenQueue(printerName);
        queue.Refresh();

        var job = queue.GetPrintJobInfoCollection()
            .FirstOrDefault(item =>
                item.Name.Equals(documentName, StringComparison.OrdinalIgnoreCase));

        return job is null ? null : Snapshot(printerName, job);
    }

    public static SpoolerJobSnapshot? GetStatus(
        string printerName,
        int jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        using var queue = OpenQueue(printerName);
        queue.Refresh();

        try
        {
            var job = queue.GetJob(jobId);
            job.Refresh();
            return Snapshot(printerName, job);
        }
        catch (PrintJobException)
        {
            return null;
        }
    }

    private static PrintQueue OpenQueue(string printerName)
    {
        var server = new LocalPrintServer();
        return server.GetPrintQueue(printerName);
    }

    private static SpoolerJobSnapshot Snapshot(
        string printerName,
        PrintSystemJobInfo job) =>
        new(
            PrinterName: printerName,
            JobId: job.JobIdentifier,
            DocumentName: job.Name,
            State: Map(job),
            Position: job.PositionInPrintQueue,
            StatusText: job.JobStatus.ToString());

    private static SpoolerJobState Map(PrintSystemJobInfo job)
    {
        if (job.IsDeleted || job.IsDeleting)
            return SpoolerJobState.Deleted;

        if (job.IsInError ||
            job.IsOffline ||
            job.IsPaperOut ||
            job.IsBlocked ||
            job.IsUserInterventionRequired)
            return SpoolerJobState.Error;

        if (job.IsPrinting)
            return SpoolerJobState.Printing;

        if (job.IsCompleted || job.IsPrinted)
            return SpoolerJobState.Completed;

        if (job.IsSpooling || job.IsQueued || job.IsRetained)
            return SpoolerJobState.Queued;

        return SpoolerJobState.Unknown;
    }
}
