using System.IO;
using System.Security.Cryptography;
using System.Text;
using PrintAI.DocumentConversion;
using PrintAI.Spreadsheet;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    private readonly Dictionary<string, string> _officeSourceByConvertedPath =
        new(StringComparer.OrdinalIgnoreCase);

    private string? _excelLastPlanSummary;

    public async Task ApplyExcelSmartPrintAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException(
                "Chọn một Excel source trước.");

        if (!TryGetExcelSource(
                page.SourcePath,
                out var sourcePath))
        {
            throw new InvalidOperationException(
                "Source đang chọn không phải XLSX có thể dùng Excel Smart Print.");
        }

        var profile =
            SpreadsheetWorkbookInspector.Inspect(sourcePath);

        var effectiveRequest =
            string.IsNullOrWhiteSpace(request)
                ? "In A4 dễ đọc, không làm chữ quá nhỏ, tự chia trang ngang khi cần."
                : request.Trim();

        var plan =
            SpreadsheetPrintHeuristics.CreatePlan(profile);

        var planningMode = "heuristic";
        var plannerWarnings =
            new List<string>(plan.Warnings);

        if (_planner.IsConfigured)
        {
            try
            {
                var ai = await _planner.PlanSpreadsheetAsync(
                    effectiveRequest,
                    profile,
                    cancellationToken);

                plan = ai.Plan;
                planningMode = "AI";
                plannerWarnings.AddRange(ai.Warnings);
            }
            catch (Exception ex)
            {
                plannerWarnings.Add(
                    $"AI planner fallback: {ex.Message}");
            }
        }

        SpreadsheetPrintPlanValidator.Validate(
            plan,
            profile);

        var hash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    Path.GetFullPath(sourcePath))))[..12];

        var outputDirectory = Path.Combine(
            _workDir,
            "smart-excel",
            hash);

        Directory.CreateDirectory(outputDirectory);

        var optimizedPath = Path.Combine(
            outputDirectory,
            "optimized.xlsx");

        SpreadsheetWorkbookOptimizer.Optimize(
            sourcePath,
            optimizedPath,
            plan);

        var convertedDirectory = Path.Combine(
            outputDirectory,
            "pdf");

        var pdfPath =
            OfficeDocumentConverter.ConvertToPdf(
                optimizedPath,
                convertedDirectory);

        var oldPath = page.SourcePath;
        var sourceIndex = _paths.FindIndex(path =>
            string.Equals(
                path,
                oldPath,
                StringComparison.OrdinalIgnoreCase));

        if (sourceIndex < 0)
            throw new InvalidOperationException(
                "Không tìm thấy converted Excel source trong session.");

        _paths[sourceIndex] = pdfPath;
        _officeSourceByConvertedPath.Remove(oldPath);
        RegisterOfficeSource(
            pdfPath,
            sourcePath);

        RebuildPages();

        _selectedPage = Math.Max(
            0,
            _pages.FindIndex(item =>
                string.Equals(
                    item.SourcePath,
                    pdfPath,
                    StringComparison.OrdinalIgnoreCase)));

        ResetPlan();
        RebuildPreview();

        var landscapeCount =
            plan.Sheets.Count(sheet =>
                sheet.Orientation ==
                SpreadsheetPageOrientation.Landscape);

        var minFont =
            plan.Sheets.Count == 0
                ? 0
                : plan.Sheets.Min(sheet =>
                    sheet.MinimumFontPt);

        _excelLastPlanSummary =
            $"{planningMode} · {plan.Sheets.Count} sheet · " +
            $"{landscapeCount} landscape · font ≥ {minFont:0.#}pt · " +
            $"output {_outputPageCount} trang PDF.";

        _status =
            "Excel Smart Print xong: " +
            _excelLastPlanSummary +
            (plannerWarnings.Count > 0
                ? " " + string.Join(
                    " | ",
                    plannerWarnings.Take(2))
                : "");
    }

    private DesktopExcelSmartPrintView BuildExcelSmartPrintView()
    {
        var page = CurrentPage();

        if (page is null ||
            !TryGetExcelSource(
                page.SourcePath,
                out var sourcePath))
        {
            return new(
                false,
                null,
                0,
                0,
                _excelLastPlanSummary);
        }

        try
        {
            var profile =
                SpreadsheetWorkbookInspector.Inspect(sourcePath);

            return new(
                true,
                Path.GetFileName(sourcePath),
                profile.Sheets.Count,
                profile.Sheets.Count(sheet =>
                    sheet.IsWide),
                _excelLastPlanSummary);
        }
        catch
        {
            return new(
                false,
                Path.GetFileName(sourcePath),
                0,
                0,
                _excelLastPlanSummary);
        }
    }

    private void RegisterOfficeSource(
        string convertedPath,
        string originalPath)
    {
        if (!Path.GetExtension(originalPath)
                .Equals(
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _officeSourceByConvertedPath[
            Path.GetFullPath(convertedPath)] =
            Path.GetFullPath(originalPath);
    }

    private bool TryGetExcelSource(
        string convertedPath,
        out string sourcePath)
    {
        return _officeSourceByConvertedPath.TryGetValue(
            Path.GetFullPath(convertedPath),
            out sourcePath!);
    }

    private void ClearExcelSmartPrintState()
    {
        _officeSourceByConvertedPath.Clear();
        _excelLastPlanSummary = null;
    }
}
