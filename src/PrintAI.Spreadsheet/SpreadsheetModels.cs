namespace PrintAI.Spreadsheet;

public enum SpreadsheetPageOrientation
{
    Portrait,
    Landscape
}

public sealed record SpreadsheetColumnProfile(
    int Index,
    string Header,
    int MaxTextLength,
    bool Hidden);

public sealed record SpreadsheetSheetProfile(
    string Name,
    uint MaxRow,
    int MaxColumn,
    double EstimatedWidthChars,
    int MergedCellCount,
    int HiddenColumnCount,
    int HeaderRow,
    IReadOnlyList<SpreadsheetColumnProfile> Columns)
{
    public bool IsWide =>
        MaxColumn >= 8 ||
        EstimatedWidthChars >= 75;
}

public sealed record SpreadsheetWorkbookProfile(
    string FileName,
    IReadOnlyList<SpreadsheetSheetProfile> Sheets);

public sealed record SpreadsheetSheetPrintPlan(
    string SheetName,
    SpreadsheetPageOrientation Orientation,
    double MinimumFontPt,
    int MaxColumnWidthChars,
    bool WrapText,
    int RepeatHeaderRows,
    int RepeatLeadingColumns,
    int ScalePercent);

public sealed record SpreadsheetPrintPlan(
    string SchemaVersion,
    IReadOnlyList<SpreadsheetSheetPrintPlan> Sheets,
    IReadOnlyList<string> Warnings)
{
    public const string CurrentSchemaVersion = "1.0";
}

public sealed record SpreadsheetOptimizeResult(
    string OutputPath,
    SpreadsheetPrintPlan Plan);
