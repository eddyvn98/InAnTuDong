namespace PrintAI.Spreadsheet;

public static class SpreadsheetPrintHeuristics
{
    public static SpreadsheetPrintPlan CreatePlan(
        SpreadsheetWorkbookProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var sheets = profile.Sheets
            .Where(sheet => sheet.MaxColumn > 0)
            .Select(CreateSheetPlan)
            .ToArray();

        var warnings = new List<string>();

        if (sheets.Length == 0)
            warnings.Add("Workbook không có sheet chứa dữ liệu để tối ưu.");

        if (profile.Sheets.Any(sheet => sheet.MergedCellCount > 0))
        {
            warnings.Add(
                "Workbook có merged cells; cần kiểm tra preview vì merged cells có thể ảnh hưởng phân trang.");
        }

        return new(
            SpreadsheetPrintPlan.CurrentSchemaVersion,
            sheets,
            warnings);
    }

    private static SpreadsheetSheetPrintPlan CreateSheetPlan(
        SpreadsheetSheetProfile sheet)
    {
        var landscape = sheet.IsWide;
        var headerCoverage = sheet.MaxColumn == 0
            ? 0
            : (double)sheet.Columns.Count(column =>
                !string.IsNullOrWhiteSpace(column.Header)) /
              sheet.MaxColumn;

        var repeatHeaderRows =
            sheet.HeaderRow > 0 &&
            headerCoverage >= 0.35
                ? 1
                : 0;

        var repeatLeadingColumns = landscape
            ? sheet.MaxColumn >= 12
                ? 2
                : 1
            : 0;

        return new(
            sheet.Name,
            landscape
                ? SpreadsheetPageOrientation.Landscape
                : SpreadsheetPageOrientation.Portrait,
            MinimumFontPt: 9.5,
            MaxColumnWidthChars: landscape ? 28 : 24,
            WrapText: true,
            RepeatHeaderRows: repeatHeaderRows,
            RepeatLeadingColumns: repeatLeadingColumns,
            ScalePercent: 100);
    }
}

public static class SpreadsheetPrintPlanValidator
{
    public static void Validate(
        SpreadsheetPrintPlan plan,
        SpreadsheetWorkbookProfile profile)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(profile);

        if (plan.SchemaVersion != SpreadsheetPrintPlan.CurrentSchemaVersion)
            throw new ArgumentException("Unsupported SpreadsheetPrintPlan schema version.");

        var knownSheets = profile.Sheets
            .Select(sheet => sheet.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (plan.Sheets.Count == 0)
            throw new ArgumentException("Spreadsheet print plan must contain at least one sheet.");

        foreach (var sheet in plan.Sheets)
        {
            if (!knownSheets.Contains(sheet.SheetName))
                throw new ArgumentException($"Unknown worksheet: {sheet.SheetName}");

            if (sheet.MinimumFontPt is < 8.5 or > 14)
                throw new ArgumentOutOfRangeException(nameof(plan), "Minimum font must be 8.5-14 pt.");

            if (sheet.MaxColumnWidthChars is < 10 or > 45)
                throw new ArgumentOutOfRangeException(nameof(plan), "Max column width must be 10-45 characters.");

            if (sheet.RepeatHeaderRows is < 0 or > 3)
                throw new ArgumentOutOfRangeException(nameof(plan), "Repeat header rows must be 0-3.");

            if (sheet.RepeatLeadingColumns is < 0 or > 3)
                throw new ArgumentOutOfRangeException(nameof(plan), "Repeat leading columns must be 0-3.");

            if (sheet.ScalePercent is < 90 or > 100)
                throw new ArgumentOutOfRangeException(nameof(plan), "Scale must be 90-100 percent.");

            if (sheet.MinimumFontPt * sheet.ScalePercent / 100d < 8.5)
                throw new ArgumentException("Effective printed font would be below 8.5 pt.");
        }
    }
}
