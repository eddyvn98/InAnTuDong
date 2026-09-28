using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PrintAI.Spreadsheet;

public static partial class SpreadsheetWorkbookOptimizer
{
    private static void ApplyColumnWidths(
        Worksheet worksheet,
        SheetData sheetData,
        SpreadsheetSheetProfile profile,
        SpreadsheetSheetPrintPlan plan)
    {
        var existing = worksheet.GetFirstChild<Columns>();
        var hiddenColumns = new HashSet<int>();

        if (existing is not null)
        {
            foreach (var column in existing.Elements<Column>())
            {
                if (column.Hidden?.Value != true)
                    continue;

                var min = (int)(column.Min?.Value ?? 1u);
                var max = (int)(column.Max?.Value ?? (uint)min);

                for (var index = min;
                     index <= Math.Min(max, profile.MaxColumn);
                     index++)
                {
                    hiddenColumns.Add(index);
                }
            }

            existing.Remove();
        }

        var columns = new Columns();

        foreach (var item in profile.Columns)
        {
            var width = Math.Clamp(
                Math.Max(8, item.MaxTextLength + 2),
                8,
                plan.MaxColumnWidthChars);

            columns.Append(new Column
            {
                Min = (uint)item.Index,
                Max = (uint)item.Index,
                Width = width,
                CustomWidth = true,
                BestFit = true,
                Hidden = hiddenColumns.Contains(item.Index)
            });
        }

        worksheet.InsertBefore(
            columns,
            sheetData);
    }

    private static void ApplyPageSetup(
        Worksheet worksheet,
        SpreadsheetSheetPrintPlan plan)
    {
        var sheetProperties =
            worksheet.GetFirstChild<SheetProperties>();

        if (sheetProperties is null)
        {
            sheetProperties = new SheetProperties();
            worksheet.PrependChild(sheetProperties);
        }

        sheetProperties.PageSetupProperties =
            new PageSetupProperties
            {
                FitToPage = false,
                AutoPageBreaks = true
            };

        var pageMargins =
            worksheet.GetFirstChild<PageMargins>();

        if (pageMargins is null)
        {
            pageMargins = new PageMargins();
            worksheet.Append(pageMargins);
        }

        pageMargins.Left = 0.25;
        pageMargins.Right = 0.25;
        pageMargins.Top = 0.35;
        pageMargins.Bottom = 0.35;
        pageMargins.Header = 0.15;
        pageMargins.Footer = 0.15;

        var pageSetup =
            worksheet.GetFirstChild<PageSetup>();

        if (pageSetup is null)
        {
            pageSetup = new PageSetup();
            worksheet.Append(pageSetup);
        }

        pageSetup.PaperSize = 9u;
        pageSetup.Orientation =
            plan.Orientation ==
            SpreadsheetPageOrientation.Landscape
                ? OrientationValues.Landscape
                : OrientationValues.Portrait;
        pageSetup.Scale =
            (uint)plan.ScalePercent;
        pageSetup.FitToWidth = 0u;
        pageSetup.FitToHeight = 0u;
    }

    private static void ApplyDefinedNames(
        WorkbookPart workbookPart,
        int sheetIndex,
        SpreadsheetSheetProfile profile,
        SpreadsheetSheetPrintPlan plan)
    {
        var workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Workbook is missing.");
        var definedNames = workbook.DefinedNames;

        if (definedNames is null)
        {
            definedNames = new DefinedNames();
            workbook.Append(definedNames);
        }

        RemoveDefinedName(
            definedNames,
            "_xlnm.Print_Area",
            sheetIndex);

        RemoveDefinedName(
            definedNames,
            "_xlnm.Print_Titles",
            sheetIndex);

        var sheetName = QuoteSheetName(
            profile.Name);

        var lastColumn =
            SpreadsheetWorkbookInspector.GetColumnName(
                profile.MaxColumn);

        var maxRow = Math.Max(
            1u,
            profile.MaxRow);

        definedNames.Append(new DefinedName
        {
            Name = "_xlnm.Print_Area",
            LocalSheetId = (uint)sheetIndex,
            Text =
                sheetName + "!$A$1:$" +
                lastColumn + "$" + maxRow
        });

        var titleParts = new List<string>();

        if (plan.RepeatHeaderRows > 0)
        {
            var startRow = Math.Max(
                1,
                profile.HeaderRow);

            var endRow = Math.Max(
                startRow,
                startRow +
                plan.RepeatHeaderRows - 1);

            titleParts.Add(
                sheetName + "!$" +
                startRow + ":$" +
                endRow);
        }

        if (plan.RepeatLeadingColumns > 0)
        {
            var endColumn =
                SpreadsheetWorkbookInspector.GetColumnName(
                    Math.Min(
                        plan.RepeatLeadingColumns,
                        profile.MaxColumn));

            titleParts.Add(
                sheetName + "!$A:$" +
                endColumn);
        }

        if (titleParts.Count > 0)
        {
            definedNames.Append(new DefinedName
            {
                Name = "_xlnm.Print_Titles",
                LocalSheetId = (uint)sheetIndex,
                Text = string.Join(
                    ",",
                    titleParts)
            });
        }
    }

    private static void RemoveDefinedName(
        DefinedNames definedNames,
        string name,
        int sheetIndex)
    {
        foreach (var item in definedNames
                     .Elements<DefinedName>()
                     .Where(item =>
                         string.Equals(
                             item.Name?.Value,
                             name,
                             StringComparison.Ordinal) &&
                         item.LocalSheetId?.Value ==
                         (uint)sheetIndex)
                     .ToArray())
        {
            item.Remove();
        }
    }

    private static string QuoteSheetName(
        string sheetName) =>
        "'" + sheetName.Replace(
            "'",
            "''") + "'";
}
