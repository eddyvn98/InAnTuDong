using System.Text.Json;
using PrintAI.Planning;
using PrintAI.Spreadsheet;
using Xunit;

namespace PrintAI.Tests;

public sealed class SpreadsheetSmartPrintPlannerTests
{
    [Fact]
    public async Task AiPlanner_AcceptsGuardedReadablePlan()
    {
        using var fixture =
            SpreadsheetFixture.Create();
        var path =
            fixture.CreateWideWorkbook();
        var profile =
            SpreadsheetWorkbookInspector.Inspect(
                path);

        var response =
            JsonSerializer.Serialize(new
            {
                plan = new
                {
                    schemaVersion = "1.0",
                    sheets = new[]
                    {
                        new
                        {
                            sheetName = "Data",
                            orientation =
                                "landscape",
                            minimumFontPt = 10.0,
                            maxColumnWidthChars = 26,
                            wrapText = true,
                            repeatHeaderRows = 1,
                            repeatLeadingColumns = 2,
                            scalePercent = 100
                        }
                    },
                    warnings =
                        Array.Empty<string>()
                },
                confidence = 0.96,
                warnings =
                    Array.Empty<string>()
            });

        var planner =
            new SpreadsheetPrintPlanner(
                new FakePlannerModelClient(
                    response));

        var result =
            await planner.PlanAsync(
                "In A4 dễ đọc, không làm chữ quá nhỏ.",
                profile);

        var sheet =
            Assert.Single(
                result.Plan.Sheets);

        Assert.Equal(
            10.0,
            sheet.MinimumFontPt);
        Assert.Equal(
            2,
            sheet.RepeatLeadingColumns);
        Assert.Equal(
            100,
            sheet.ScalePercent);
    }

    [Fact]
    public void Validator_RejectsTinyEffectiveText()
    {
        using var fixture =
            SpreadsheetFixture.Create();
        var path =
            fixture.CreateWideWorkbook();
        var profile =
            SpreadsheetWorkbookInspector.Inspect(
                path);

        var plan =
            new SpreadsheetPrintPlan(
                "1.0",
                [
                    new SpreadsheetSheetPrintPlan(
                        "Data",
                        SpreadsheetPageOrientation
                            .Landscape,
                        MinimumFontPt: 8.5,
                        MaxColumnWidthChars: 24,
                        WrapText: true,
                        RepeatHeaderRows: 1,
                        RepeatLeadingColumns: 2,
                        ScalePercent: 90)
                ],
                []);

        Assert.Throws<ArgumentException>(
            () =>
                SpreadsheetPrintPlanValidator
                    .Validate(
                        plan,
                        profile));
    }

    private sealed class FakePlannerModelClient(
        string response) : IPlannerModelClient
    {
        public Task<string> CompleteAsync(
            PlannerModelRequest request,
            CancellationToken cancellationToken =
                default) =>
            Task.FromResult(response);
    }
}
