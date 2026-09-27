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
    IReadOnlyList<DesktopScanner> Scanners,
    string? SelectedScanner,
    IReadOnlyList<DesktopRecipe> Recipes,
    string? SelectedRecipeId,
    IReadOnlyList<DesktopWorkflowPreset> BuiltInWorkflows,
    string? SelectedWorkflowId,
    string? PreviewDataUrl,
    string? Status,
    bool CanPrint,
    bool CanPrintJob,
    bool CanPrintAllSources,
    DesktopPlannerView Planner,
    DesktopExcelSmartPrintView ExcelSmartPrint,
    DesktopReadinessView Readiness,
    IReadOnlyList<JobHistoryEntry> History);

public sealed record DesktopExcelSmartPrintView(
    bool Available,
    string? SourceName,
    int SheetCount,
    int WideSheetCount,
    string? LastPlanSummary);

public sealed record DesktopReadinessView(
    bool ReadyForCorePrinting,
    int PassCount,
    int WarningCount,
    int FailureCount,
    IReadOnlyList<DesktopReadinessCheck> Checks);

public sealed record DesktopReadinessCheck(
    string Id,
    string Name,
    string State,
    string Detail);

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
    string Fit,
    bool CopiesArePerSource)
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
            job.Layout.Fit.ToString(),
            job.Sources.Count > 1);
}

public sealed record DesktopCompositionItem(
    int PageIndex,
    int Copies);

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

public sealed record DesktopScanner(
    string Id,
    string Name);

public sealed record DesktopRecipe(
    string Id,
    string Name,
    bool DirectPrintEligible,
    DateTimeOffset? UpdatedAt);

public sealed record DesktopWorkflowPreset(
    string Id,
    string Name,
    string Category,
    string Description,
    double ItemWidthMm,
    double ItemHeightMm,
    int SourceCopies);
