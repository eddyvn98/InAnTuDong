using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PrintAI.Spreadsheet;

public static partial class SpreadsheetWorkbookOptimizer
{
    public static SpreadsheetOptimizeResult Optimize(
        string inputPath,
        string outputPath,
        SpreadsheetPrintPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(plan);

        var profile = SpreadsheetWorkbookInspector.Inspect(inputPath);
        SpreadsheetPrintPlanValidator.Validate(plan, profile);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.Copy(inputPath, outputPath, overwrite: true);

        using var document = SpreadsheetDocument.Open(outputPath, true);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Workbook part is missing.");

        EnsureStyles(workbookPart);

        var workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Workbook is missing.");

        var sheets = workbook.Sheets?
            .Elements<Sheet>()
            .ToArray() ?? [];

        var planByName = plan.Sheets.ToDictionary(
            item => item.SheetName,
            StringComparer.Ordinal);

        for (var sheetIndex = 0; sheetIndex < sheets.Length; sheetIndex++)
        {
            var sheet = sheets[sheetIndex];
            var relationshipId = sheet.Id?.Value;
            var sheetName = sheet.Name?.Value;

            if (string.IsNullOrWhiteSpace(relationshipId) ||
                string.IsNullOrWhiteSpace(sheetName) ||
                !planByName.TryGetValue(
                    sheetName,
                    out var sheetPlan))
            {
                continue;
            }

            var worksheetPart =
                (WorksheetPart)workbookPart.GetPartById(relationshipId);

            var sheetProfile = profile.Sheets.First(
                item => string.Equals(
                    item.Name,
                    sheetName,
                    StringComparison.Ordinal));

            ApplySheetPlan(
                workbookPart,
                worksheetPart,
                sheetIndex,
                sheetProfile,
                sheetPlan);
        }

        workbook.Save();

        return new(
            outputPath,
            plan);
    }

    private static void ApplySheetPlan(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart,
        int sheetIndex,
        SpreadsheetSheetProfile profile,
        SpreadsheetSheetPrintPlan plan)
    {
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException("Worksheet is missing.");
        var sheetData = worksheet.GetFirstChild<SheetData>();

        if (sheetData is null || profile.MaxColumn == 0)
            return;

        ApplyColumnWidths(
            worksheet,
            sheetData,
            profile,
            plan);

        ApplyReadableStyles(
            workbookPart,
            sheetData,
            plan);

        ApplyPageSetup(
            worksheet,
            plan);

        ApplyDefinedNames(
            workbookPart,
            sheetIndex,
            profile,
            plan);

        worksheet.Save();
    }
}
