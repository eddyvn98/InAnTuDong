using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PrintAI.Spreadsheet;

namespace PrintAI.Desktop;

internal static partial class DesktopSelfTest
{
    private static void CheckSpreadsheetPipeline(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory)
    {
        var sourcePath = Path.Combine(
            tempDirectory,
            "smart-print.xlsx");
        var outputPath = Path.Combine(
            tempDirectory,
            "smart-print-optimized.xlsx");

        CreateSmokeWorkbook(sourcePath);

        var profile =
            SpreadsheetWorkbookInspector.Inspect(
                sourcePath);
        var plan =
            SpreadsheetPrintHeuristics.CreatePlan(
                profile);

        SpreadsheetWorkbookOptimizer.Optimize(
            sourcePath,
            outputPath,
            plan);

        var sheet = profile.Sheets.FirstOrDefault();
        var sheetPlan = plan.Sheets.FirstOrDefault();

        var passed =
            File.Exists(outputPath) &&
            sheet is not null &&
            sheet.MaxColumn == 10 &&
            sheetPlan is not null &&
            sheetPlan.Orientation ==
                SpreadsheetPageOrientation.Landscape &&
            sheetPlan.ScalePercent >= 90 &&
            sheetPlan.MinimumFontPt >= 8.5;

        checks.Add(new(
            "spreadsheet-smart-print",
            passed,
            passed
                ? "XLSX inspect -> readable plan -> optimized workbook succeeded."
                : "XLSX Smart Print pipeline did not produce the expected readable plan/output."));
    }

    private static void CreateSmokeWorkbook(
        string path)
    {
        using var document =
            SpreadsheetDocument.Create(
                path,
                SpreadsheetDocumentType.Workbook);

        var workbookPart =
            document.AddWorkbookPart();
        workbookPart.Workbook =
            new Workbook();

        var worksheetPart =
            workbookPart
                .AddNewPart<WorksheetPart>();

        var data = new SheetData();
        worksheetPart.Worksheet =
            new Worksheet(data);

        var sheets =
            workbookPart.Workbook
                .AppendChild(
                    new Sheets());

        sheets.Append(
            new Sheet
            {
                Id = workbookPart.GetIdOfPart(
                    worksheetPart),
                SheetId = 1,
                Name = "Data"
            });

        data.Append(
            CreateSmokeRow(
                1,
                Enumerable.Range(1, 10)
                    .Select(index =>
                        index == 1
                            ? "STT"
                            : "Column " + index)));

        data.Append(
            CreateSmokeRow(
                2,
                Enumerable.Range(1, 10)
                    .Select(index =>
                        index == 1
                            ? "1"
                            : "Long printable value " +
                              index)));

        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    private static Row CreateSmokeRow(
        uint rowIndex,
        IEnumerable<string> values)
    {
        var row =
            new Row
            {
                RowIndex = rowIndex
            };

        var column = 1;

        foreach (var value in values)
        {
            row.Append(
                new Cell
                {
                    CellReference =
                        SpreadsheetWorkbookInspector
                            .GetColumnName(column) +
                        rowIndex,
                    DataType =
                        CellValues.InlineString,
                    InlineString =
                        new InlineString(
                            new Text(value))
                });

            column++;
        }

        return row;
    }
}
