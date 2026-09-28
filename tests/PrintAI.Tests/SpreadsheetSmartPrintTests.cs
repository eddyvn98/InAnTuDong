using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PrintAI.Planning;
using PrintAI.Spreadsheet;
using Xunit;

namespace PrintAI.Tests;

public sealed class SpreadsheetSmartPrintTests
{
    [Fact]
    public void InspectAndHeuristics_WideWorkbookPrefersReadableLandscape()
    {
        using var fixture = SpreadsheetFixture.Create();
        var path = fixture.CreateWideWorkbook();

        var profile = SpreadsheetWorkbookInspector.Inspect(path);
        var plan = SpreadsheetPrintHeuristics.CreatePlan(profile);

        var sheet = Assert.Single(profile.Sheets);
        var sheetPlan = Assert.Single(plan.Sheets);

        Assert.Equal(12, sheet.MaxColumn);
        Assert.True(sheet.IsWide);
        Assert.Equal(SpreadsheetPageOrientation.Landscape, sheetPlan.Orientation);
        Assert.True(sheetPlan.MinimumFontPt >= 9.5);
        Assert.Equal(100, sheetPlan.ScalePercent);
        Assert.True(sheetPlan.WrapText);
        Assert.Equal(1, sheetPlan.RepeatHeaderRows);
        Assert.Equal(2, sheetPlan.RepeatLeadingColumns);
    }

    [Fact]
    public void Optimizer_WritesReadablePrintSettingsWithoutFitAllColumns()
    {
        using var fixture = SpreadsheetFixture.Create();
        var input = fixture.CreateWideWorkbook();
        var output = fixture.PathFor("optimized.xlsx");

        var profile = SpreadsheetWorkbookInspector.Inspect(input);
        var plan = SpreadsheetPrintHeuristics.CreatePlan(profile);

        SpreadsheetWorkbookOptimizer.Optimize(
            input,
            output,
            plan);

        using var document = SpreadsheetDocument.Open(output, false);
        var workbookPart = Assert.IsType<WorkbookPart>(document.WorkbookPart);
        var workbook = Assert.IsType<Workbook>(workbookPart.Workbook);
        var sheets = Assert.IsType<Sheets>(workbook.Sheets);
        var sheet = Assert.Single(sheets.Elements<Sheet>());
        var worksheetPart = Assert.IsType<WorksheetPart>(
            workbookPart.GetPartById(sheet.Id!.Value!));
        var worksheet = Assert.IsType<Worksheet>(worksheetPart.Worksheet);

        var pageSetup = Assert.IsType<PageSetup>(
            worksheet.GetFirstChild<PageSetup>());

        Assert.Equal(
            OrientationValues.Landscape,
            pageSetup.Orientation?.Value);
        Assert.Equal((uint)100, pageSetup.Scale?.Value);
        Assert.Equal((uint)0, pageSetup.FitToWidth?.Value);
        Assert.Equal((uint)0, pageSetup.FitToHeight?.Value);

        var columns = Assert.IsType<Columns>(
            worksheet.GetFirstChild<Columns>());

        Assert.Equal(12, columns.Elements<Column>().Count());
        Assert.All(
            columns.Elements<Column>(),
            column =>
            {
                Assert.True(column.Width?.Value >= 8);
                Assert.True(column.Width?.Value <= 28);
            });

        var definedNames =
            workbookPart.Workbook.DefinedNames!
                .Elements<DefinedName>()
                .ToArray();

        Assert.Contains(
            definedNames,
            item =>
                item.Name?.Value == "_xlnm.Print_Area" &&
                item.Text.Contains("$A$1:$L$6"));

        Assert.Contains(
            definedNames,
            item =>
                item.Name?.Value == "_xlnm.Print_Titles" &&
                item.Text.Contains("$1:$1") &&
                item.Text.Contains("$A:$B"));

        var firstCell = worksheet
            .Descendants<Cell>()
            .First();

        Assert.True((firstCell.StyleIndex?.Value ?? 0) > 0);
    }

    [Fact]
    public async Task AiPlanner_AcceptsGuardedReadablePlan()
    {
        using var fixture = SpreadsheetFixture.Create();
        var path = fixture.CreateWideWorkbook();
        var profile = SpreadsheetWorkbookInspector.Inspect(path);

        var response = JsonSerializer.Serialize(new
        {
            plan = new
            {
                schemaVersion = "1.0",
                sheets = new[]
                {
                    new
                    {
                        sheetName = "Data",
                        orientation = "landscape",
                        minimumFontPt = 10.0,
                        maxColumnWidthChars = 26,
                        wrapText = true,
                        repeatHeaderRows = 1,
                        repeatLeadingColumns = 2,
                        scalePercent = 100
                    }
                },
                warnings = Array.Empty<string>()
            },
            confidence = 0.96,
            warnings = Array.Empty<string>()
        });

        var planner = new SpreadsheetPrintPlanner(
            new FakePlannerModelClient(response));

        var result = await planner.PlanAsync(
            "In A4 dễ đọc, không làm chữ quá nhỏ.",
            profile);

        var sheet = Assert.Single(result.Plan.Sheets);
        Assert.Equal(10.0, sheet.MinimumFontPt);
        Assert.Equal(2, sheet.RepeatLeadingColumns);
        Assert.Equal(100, sheet.ScalePercent);
    }

    [Fact]
    public void Validator_RejectsTinyEffectiveText()
    {
        using var fixture = SpreadsheetFixture.Create();
        var path = fixture.CreateWideWorkbook();
        var profile = SpreadsheetWorkbookInspector.Inspect(path);

        var plan = new SpreadsheetPrintPlan(
            "1.0",
            [
                new SpreadsheetSheetPrintPlan(
                    "Data",
                    SpreadsheetPageOrientation.Landscape,
                    MinimumFontPt: 8.5,
                    MaxColumnWidthChars: 24,
                    WrapText: true,
                    RepeatHeaderRows: 1,
                    RepeatLeadingColumns: 2,
                    ScalePercent: 90)
            ],
            []);

        Assert.Throws<ArgumentException>(() =>
            SpreadsheetPrintPlanValidator.Validate(
                plan,
                profile));
    }

    private sealed class FakePlannerModelClient(
        string response) : IPlannerModelClient
    {
        public Task<string> CompleteAsync(
            PlannerModelRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class SpreadsheetFixture : IDisposable
    {
        private SpreadsheetFixture(string directory)
        {
            Directory = directory;
        }

        public string Directory { get; }

        public static SpreadsheetFixture Create()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"printai-xlsx-{Guid.NewGuid():N}");

            System.IO.Directory.CreateDirectory(directory);
            return new(directory);
        }

        public string PathFor(string name) =>
            Path.Combine(Directory, name);

        public string CreateWideWorkbook()
        {
            var path = PathFor("wide.xlsx");

            using var document = SpreadsheetDocument.Create(
                path,
                SpreadsheetDocumentType.Workbook);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart =
                workbookPart.AddNewPart<WorksheetPart>();

            var sheetData = new SheetData();
            worksheetPart.Worksheet =
                new Worksheet(sheetData);

            var sheets =
                workbookPart.Workbook.AppendChild(
                    new Sheets());

            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(
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

            for (uint row = 2; row <= 6; row++)
            {
                sheetData.Append(
                    CreateRow(
                        row,
                        headers.Select(
                            (header, index) =>
                                index == 0
                                    ? (row - 1).ToString()
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
            var row = new Row
            {
                RowIndex = rowIndex
            };

            var column = 1;

            foreach (var value in values)
            {
                row.Append(new Cell
                {
                    CellReference =
                        SpreadsheetWorkbookInspector
                            .GetColumnName(column) +
                        rowIndex,
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(
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
}
