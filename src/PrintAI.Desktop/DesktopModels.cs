using PrintAI.Domain;
using PrintAI.History;

namespace PrintAI.Desktop;

public sealed record DesktopState(
    IReadOnlyList<DesktopFile> Files,
    IReadOnlyList<DesktopPage> Pages,
    int SelectedPage,
    int OutputPageCount,
    int SelectedOutputPage,
    IReadOnlyList<DesktopPrinter> Printers,
    string? SelectedPrinter,
    string? PreviewDataUrl,
    string? Status,
    bool CanPrint,
    bool CanPrintJob,
    bool CanPrintAllSources,
    DesktopPlannerView Planner,
    IReadOnlyList<JobHistoryEntry> History);

public sealed record DesktopPlannerView(
    bool Configured,
    string? Endpoint,
    string? Model,
    string? Request,
    string? Decision,
    double? Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    DesktopJobView? Job);

public sealed record DesktopJobView(
    string Mode,
    double ItemWidthMm,
    double ItemHeightMm,
    double GapMm,
    double MarginMm,
    int Copies,
    bool AllowRotate,
    bool CutMarks,
    string Fit)
{
    public static DesktopJobView From(PrintJobSpec job) =>
        new(
            job.Layout.Mode.ToString(),
            job.Layout.ItemWidthMm,
            job.Layout.ItemHeightMm,
            job.Layout.GapMm,
            job.Layout.MarginMm,
            job.Sources.FirstOrDefault()?.Copies ?? 1,
            job.Layout.AllowRotate,
            job.Layout.CutMarks,
            job.Layout.Fit.ToString());
}

public sealed record DesktopPage(
    int GlobalIndex,
    string SourcePath,
    string SourceName,
    int SourcePageIndex,
    string PageLabel);

public sealed record DesktopFile(
    string Name,
    string Path,
    string Kind,
    int? PixelWidth,
    int? PixelHeight,
    int? PageCount,
    string? Error);

public sealed record DesktopPrinter(
    string Name,
    bool IsDefault,
    bool SupportsColor,
    bool CanDuplex);
