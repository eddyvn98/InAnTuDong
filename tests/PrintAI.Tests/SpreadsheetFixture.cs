using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PrintAI.Spreadsheet;

namespace PrintAI.Tests;

internal sealed class SpreadsheetFixture :
    IDisposable
{
    private SpreadsheetFixture(
        string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    public static SpreadsheetFixture Create()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"printai-xlsx-{Guid.NewGuid():N}");

        System.IO.Directory.CreateDirectory(
            directory);

        return new(directory);
    }

    public string PathFor(string name) =>
        Path.Combine(
            Directory,
            name);

    public string CreateWideWorkbook()
    {
        var path =
            PathFor("wide.xlsx");

        using var document =
            SpreadsheetDocument.Create(
                path,
                SpreadsheetDocumentType
                    .Workbook);

        var workbookPart =
            document.AddWorkbookPart();
        workbookPart.Workbook =
            new Workbook();

        var worksheetPart =
            workbookPart
                .AddNewPart<WorksheetPart>();

        var sheetData =
            new SheetData();

        worksheetPart.Worksheet =
            new Worksheet(sheetData);

        var sheets =
            workbookPart.Workbook
                .AppendChild(
                    new Sheets());

        sheets.Append(
            new Sheet
            {
                Id =
                    workbookPart
                        .GetIdOfPart(
                            worksheetPart),
                SheetId = 1,
                Name = "Data"
            });

        var headers = new[]
        {
            "STT",
            "Tên nhân viên",
            "Bộ phận",
            "Mã nhân viên",
            "Ngày",
            "Nội dung công việc rất dài",
            "Trạng thái",
            "Người phụ trách",
            "Ngày hoàn thành",
            "Ghi chú chi tiết",
            "Mã dự án",
            "Thông tin bổ sung"
        };

        sheetData.Append(
            CreateRow(
                1,
                headers));

        for (uint row = 2;
             row <= 6;
             row++)
        {
            sheetData.Append(
                CreateRow(
                    row,
                    headers.Select(
                        (header, index) =>
                            index == 0
                                ? (row - 1)
                                    .ToString()
                                : $"{header} - dữ liệu dòng {row}")));
        }

        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();

        return path;
    }

    private static Row CreateRow(
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
                            .GetColumnName(
                                column) +
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

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(
                Directory,
                recursive: true);
        }
        catch
        {
        }
    }
}
