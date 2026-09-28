using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
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
        Assert.Equal(
            SpreadsheetPageOrientation.Landscape,
            sheetPlan.Orientation);
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

        using var document =
            SpreadsheetDocument.Open(output, false);
        var workbookPart =
            Assert.IsType<WorkbookPart>(
                document.WorkbookPart);
        var workbook =
            Assert.IsType<Workbook>(
                workbookPart.Workbook);
        var sheets =
            Assert.IsType<Sheets>(
                workbook.Sheets);
        var sheet =
            Assert.Single(
                sheets.Elements<Sheet>());
        var worksheetPart =
            Assert.IsType<WorksheetPart>(
                workbookPart.GetPartById(
                    sheet.Id!.Value!));
        var worksheet =
            Assert.IsType<Worksheet>(
                worksheetPart.Worksheet);

        var pageSetup =
            Assert.IsType<PageSetup>(
                worksheet
                    .GetFirstChild<PageSetup>());

        Assert.Equal(
            OrientationValues.Landscape,
            pageSetup.Orientation?.Value);
        Assert.Equal(
            (uint)100,
            pageSetup.Scale?.Value);
        Assert.Equal(
            (uint)0,
            pageSetup.FitToWidth?.Value);
        Assert.Equal(
            (uint)0,
            pageSetup.FitToHeight?.Value);

        var columns =
            Assert.IsType<Columns>(
                worksheet
                    .GetFirstChild<Columns>());

        Assert.Equal(
            12,
            columns.Elements<Column>().Count());

        Assert.All(
            columns.Elements<Column>(),
            column =>
            {
                Assert.True(
                    column.Width?.Value >= 8);
                Assert.True(
                    column.Width?.Value <= 28);
            });

        var definedNames =
            workbook.DefinedNames!
                .Elements<DefinedName>()
                .ToArray();

        Assert.Contains(
            definedNames,
            item =>
                item.Name?.Value ==
                    "_xlnm.Print_Area" &&
                item.Text.Contains(
                    "$A$1:$L$6"));

        Assert.Contains(
            definedNames,
            item =>
                item.Name?.Value ==
                    "_xlnm.Print_Titles" &&
                item.Text.Contains("$1:$1") &&
                item.Text.Contains("$A:$B"));

        var firstCell =
            worksheet
                .Descendants<Cell>()
                .First();

        Assert.True(
            (firstCell.StyleIndex?.Value ?? 0) >
            0);
    }
}
