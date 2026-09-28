using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PrintAI.Spreadsheet;

public static class SpreadsheetWorkbookOptimizer
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

        workbookPart.Workbook.Save();

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
                var max = (int)(column.Max?.Value ?? min);

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

    private static void ApplyReadableStyles(
        WorkbookPart workbookPart,
        SheetData sheetData,
        SpreadsheetSheetPrintPlan plan)
    {
        var styles = workbookPart.WorkbookStylesPart?.Stylesheet
            ?? throw new InvalidDataException("Workbook styles are missing.");
        var cellFormats = styles.CellFormats!;
        var fonts = styles.Fonts!;

        var usedStyleIndexes = sheetData
            .Descendants<Cell>()
            .Select(cell => (int)(cell.StyleIndex?.Value ?? 0))
            .Distinct()
            .ToArray();

        var styleMap = new Dictionary<int, uint>();

        foreach (var originalIndex in usedStyleIndexes)
        {
            var original = cellFormats
                .Elements<CellFormat>()
                .ElementAtOrDefault(originalIndex)
                ?? new CellFormat();

            var originalFontId =
                (int)(original.FontId?.Value ?? 0u);

            var originalFont = fonts
                .Elements<Font>()
                .ElementAtOrDefault(originalFontId)
                ?? new Font();

            var newFont = (Font)originalFont.CloneNode(true);
            var existingSize = newFont.FontSize?.Val?.Value ?? 11d;

            if (existingSize < plan.MinimumFontPt)
                newFont.FontSize = new FontSize { Val = plan.MinimumFontPt };

            fonts.Append(newFont);
            var newFontId = (uint)(fonts.ChildElements.Count - 1);

            var newFormat =
                (CellFormat)original.CloneNode(true);

            newFormat.FontId = newFontId;
            newFormat.ApplyFont = true;

            if (plan.WrapText)
            {
                var alignment = newFormat.Alignment is null
                    ? new Alignment()
                    : (Alignment)newFormat.Alignment.CloneNode(true);

                alignment.WrapText = true;
                alignment.Vertical = VerticalAlignmentValues.Top;
                newFormat.Alignment = alignment;
                newFormat.ApplyAlignment = true;
            }

            cellFormats.Append(newFormat);
            styleMap[originalIndex] =
                (uint)(cellFormats.ChildElements.Count - 1);
        }

        fonts.Count = (uint)fonts.ChildElements.Count;
        cellFormats.Count =
            (uint)cellFormats.ChildElements.Count;

        foreach (var cell in sheetData.Descendants<Cell>())
        {
            var current =
                (int)(cell.StyleIndex?.Value ?? 0);

            if (styleMap.TryGetValue(
                    current,
                    out var mapped))
            {
                cell.StyleIndex = mapped;
            }
        }

        styles.Save();
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

        var printOptions =
            worksheet.GetFirstChild<PrintOptions>();

        if (printOptions is null)
        {
            printOptions = new PrintOptions();
            worksheet.Append(printOptions);
        }

        printOptions.Headings = false;
        printOptions.GridLines = true;
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
                sheetName + "!$A$1:$" + lastColumn + "$" + maxRow
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
                Text = string.Join(",", titleParts)
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
        "'" + sheetName.Replace("'", "''") + "'";

    private static void EnsureStyles(
        WorkbookPart workbookPart)
    {
        var part = workbookPart.WorkbookStylesPart
            ?? workbookPart.AddNewPart<WorkbookStylesPart>();

        if (part.Stylesheet is null)
            part.Stylesheet = new Stylesheet();

        part.Stylesheet.Fonts ??=
            new Fonts(new Font());

        part.Stylesheet.Fills ??=
            new Fills(
                new Fill(
                    new PatternFill
                    {
                        PatternType =
                            PatternValues.None
                    }),
                new Fill(
                    new PatternFill
                    {
                        PatternType =
                            PatternValues.Gray125
                    }));

        part.Stylesheet.Borders ??=
            new Borders(new Border());

        part.Stylesheet.CellStyleFormats ??=
            new CellStyleFormats(
                new CellFormat());

        part.Stylesheet.CellFormats ??=
            new CellFormats(
                new CellFormat());

        part.Stylesheet.CellStyles ??=
            new CellStyles(
                new CellStyle
                {
                    Name = "Normal",
                    FormatId = 0,
                    BuiltinId = 0
                });

        part.Stylesheet.Fonts.Count =
            (uint)part.Stylesheet.Fonts.ChildElements.Count;
        part.Stylesheet.Fills.Count =
            (uint)part.Stylesheet.Fills.ChildElements.Count;
        part.Stylesheet.Borders.Count =
            (uint)part.Stylesheet.Borders.ChildElements.Count;
        part.Stylesheet.CellStyleFormats.Count =
            (uint)part.Stylesheet.CellStyleFormats.ChildElements.Count;
        part.Stylesheet.CellFormats.Count =
            (uint)part.Stylesheet.CellFormats.ChildElements.Count;
        part.Stylesheet.CellStyles.Count =
            (uint)part.Stylesheet.CellStyles.ChildElements.Count;

        part.Stylesheet.Save();
    }
}
