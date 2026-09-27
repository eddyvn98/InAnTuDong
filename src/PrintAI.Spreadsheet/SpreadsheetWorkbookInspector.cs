using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PrintAI.Spreadsheet;

public static class SpreadsheetWorkbookInspector
{
    public static SpreadsheetWorkbookProfile Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("Spreadsheet does not exist.", path);

        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Excel Smart Print currently supports XLSX files.");

        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Workbook part is missing.");

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var profiles = new List<SpreadsheetSheetProfile>();

        foreach (var sheet in workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? [])
        {
            if (sheet.Id is null || sheet.Name is null)
                continue;

            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
            profiles.Add(InspectSheet(
                sheet.Name.Value,
                worksheetPart.Worksheet,
                sharedStrings));
        }

        return new(
            Path.GetFileName(path),
            profiles);
    }

    private static SpreadsheetSheetProfile InspectSheet(
        string name,
        Worksheet worksheet,
        SharedStringTable? sharedStrings)
    {
        var sheetData = worksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return new(
                name,
                0,
                0,
                0,
                0,
                0,
                0,
                []);
        }

        uint maxRow = 0;
        var maxColumn = 0;
        var lengths = new Dictionary<int, int>();
        var firstNonEmptyRow = 0;
        var headers = new Dictionary<int, string>();

        foreach (var row in sheetData.Elements<Row>())
        {
            var rowHasText = false;

            foreach (var cell in row.Elements<Cell>())
            {
                var column = GetColumnIndex(cell.CellReference?.Value);
                if (column <= 0)
                    continue;

                maxColumn = Math.Max(maxColumn, column);
                maxRow = Math.Max(maxRow, row.RowIndex?.Value ?? 0);

                var text = ReadCellText(cell, sharedStrings);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                rowHasText = true;
                var normalizedLength = Math.Min(200, text.Trim().Length);
                lengths[column] = Math.Max(
                    lengths.GetValueOrDefault(column),
                    normalizedLength);
            }

            if (rowHasText && firstNonEmptyRow == 0)
            {
                firstNonEmptyRow = (int)(row.RowIndex?.Value ?? 1);

                foreach (var cell in row.Elements<Cell>())
                {
                    var column = GetColumnIndex(cell.CellReference?.Value);
                    if (column <= 0)
                        continue;

                    var value = ReadCellText(cell, sharedStrings).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        headers[column] = value;
                }
            }
        }

        var hidden = GetHiddenColumns(worksheet, maxColumn);
        var columns = Enumerable
            .Range(1, maxColumn)
            .Select(index => new SpreadsheetColumnProfile(
                index,
                headers.GetValueOrDefault(index, string.Empty),
                lengths.GetValueOrDefault(index),
                hidden.Contains(index)))
            .ToArray();

        var estimatedWidth = columns
            .Where(column => !column.Hidden)
            .Sum(column =>
                Math.Clamp(
                    column.MaxTextLength + 2,
                    8,
                    32));

        return new(
            name,
            maxRow,
            maxColumn,
            estimatedWidth,
            worksheet.Descendants<MergeCell>().Count(),
            hidden.Count,
            firstNonEmptyRow,
            columns);
    }

    private static HashSet<int> GetHiddenColumns(
        Worksheet worksheet,
        int maxColumn)
    {
        var result = new HashSet<int>();
        var columns = worksheet.GetFirstChild<Columns>();

        if (columns is null)
            return result;

        foreach (var column in columns.Elements<Column>())
        {
            if (column.Hidden?.Value != true)
                continue;

            var min = (int)(column.Min?.Value ?? 1);
            var max = (int)(column.Max?.Value ?? min);

            for (var index = min;
                 index <= Math.Min(max, maxColumn);
                 index++)
            {
                result.Add(index);
            }
        }

        return result;
    }

    private static string ReadCellText(
        Cell cell,
        SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, out var sharedIndex) &&
            sharedStrings is not null)
        {
            return sharedStrings
                .Elements<SharedStringItem>()
                .ElementAtOrDefault(sharedIndex)?
                .InnerText ?? string.Empty;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? string.Empty;

        return cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
    }

    internal static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return 0;

        var value = 0;

        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch))
                break;

            value = (value * 26) +
                (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return value;
    }

    internal static string GetColumnName(int index)
    {
        if (index <= 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var result = string.Empty;

        while (index > 0)
        {
            index--;
            result =
                (char)('A' + (index % 26)) +
                result;
            index /= 26;
        }

        return result;
    }
}
