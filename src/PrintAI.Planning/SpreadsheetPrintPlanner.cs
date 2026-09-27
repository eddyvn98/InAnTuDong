using System.Text.Json;
using System.Text.Json.Serialization;
using PrintAI.Spreadsheet;

namespace PrintAI.Planning;

public sealed record SpreadsheetPlanningOutcome(
    SpreadsheetPrintPlan Plan,
    double Confidence,
    IReadOnlyList<string> Warnings,
    string RawJson);

public sealed class SpreadsheetPrintPlanner(
    IPlannerModelClient modelClient)
{
    private const string SystemInstruction =
        """
        You are the spreadsheet print-planning layer for Print AI.
        Return JSON only. Do not return markdown.
        Your goal is readable A4 printing, not fitting every column onto one page.
        Never make effective printed text smaller than 8.5 pt.
        Prefer landscape for wide sheets.
        Use wrap text for long cells.
        Use repeatHeaderRows for table headers.
        Use repeatLeadingColumns for identifier/name columns that should remain visible on horizontal page breaks.
        Horizontal pagination is allowed and preferred over tiny text.
        Do not invent worksheet names.
        The output must have exactly: plan, confidence, warnings.
        plan.schemaVersion must be "1.0".
        Each plan sheet must have exactly:
        sheetName, orientation, minimumFontPt, maxColumnWidthChars,
        wrapText, repeatHeaderRows, repeatLeadingColumns, scalePercent.
        orientation is portrait or landscape.
        minimumFontPt must be 8.5-14.
        maxColumnWidthChars must be 10-45.
        repeatHeaderRows must be 0-3.
        repeatLeadingColumns must be 0-3.
        scalePercent must be 90-100.
        """;

    public async Task<SpreadsheetPlanningOutcome> PlanAsync(
        string userRequest,
        SpreadsheetWorkbookProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);
        ArgumentNullException.ThrowIfNull(profile);

        var payload = JsonSerializer.Serialize(new
        {
            request = userRequest,
            workbook = new
            {
                fileName = profile.FileName,
                sheets = profile.Sheets.Select(sheet => new
                {
                    name = sheet.Name,
                    rows = sheet.MaxRow,
                    columns = sheet.MaxColumn,
                    estimatedWidthChars = sheet.EstimatedWidthChars,
                    mergedCellCount = sheet.MergedCellCount,
                    hiddenColumnCount = sheet.HiddenColumnCount,
                    headerRow = sheet.HeaderRow,
                    columns = sheet.Columns.Select(column => new
                    {
                        index = column.Index,
                        header = column.Header,
                        maxTextLength = column.MaxTextLength,
                        hidden = column.Hidden
                    })
                })
            }
        });

        var raw = await modelClient.CompleteAsync(
            new PlannerModelRequest(
                SystemInstruction,
                payload),
            cancellationToken);

        return Parse(
            raw,
            profile);
    }

    internal static SpreadsheetPlanningOutcome Parse(
        string json,
        SpreadsheetWorkbookProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        SpreadsheetPlannerEnvelope envelope;

        try
        {
            envelope =
                JsonSerializer.Deserialize<SpreadsheetPlannerEnvelope>(
                    json,
                    CreateOptions())
                ?? throw new PlanningFormatException(
                    "Spreadsheet planner returned empty JSON.");
        }
        catch (JsonException ex)
        {
            throw new PlanningFormatException(
                "Spreadsheet planner JSON does not match the required schema.",
                ex);
        }

        if (envelope.Plan is null)
            throw new PlanningFormatException("Spreadsheet planner must return plan.");

        if (envelope.Confidence is < 0 or > 1)
            throw new PlanningFormatException("Spreadsheet confidence must be between 0 and 1.");

        SpreadsheetPrintPlanValidator.Validate(
            envelope.Plan,
            profile);

        return new(
            envelope.Plan,
            envelope.Confidence,
            envelope.Warnings ?? [],
            json);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow
        };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }

    private sealed record SpreadsheetPlannerEnvelope(
        [property: JsonRequired]
        SpreadsheetPrintPlan? Plan,
        [property: JsonRequired]
        double Confidence,
        IReadOnlyList<string>? Warnings = null);
}
