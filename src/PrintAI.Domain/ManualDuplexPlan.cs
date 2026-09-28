namespace PrintAI.Domain;

public enum ManualDuplexBackOrder
{
    Forward,
    Reverse
}

public sealed record DuplexSheet(
    int SheetIndex,
    int CopyIndex,
    int FrontOutputPageIndex,
    int? BackOutputPageIndex);

public sealed record PrintSideInstruction(
    int SheetIndex,
    int CopyIndex,
    int OutputPageIndex,
    int RotationDegrees);

public sealed record ManualDuplexPlan(
    DuplexMode Mode,
    IReadOnlyList<DuplexSheet> Sheets,
    IReadOnlyList<PrintSideInstruction> FrontPass,
    IReadOnlyList<PrintSideInstruction> BackPass);

public static class ManualDuplexPlanner
{
    public static ManualDuplexPlan Create(
        int outputPageCount,
        int copies,
        DuplexMode mode,
        ManualDuplexBackOrder backOrder,
        int longEdgeBackRotationDegrees,
        int shortEdgeBackRotationDegrees)
    {
        if (outputPageCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputPageCount));

        if (copies <= 0)
            throw new ArgumentOutOfRangeException(nameof(copies));

        if (mode == DuplexMode.Off)
            throw new ArgumentException(
                "Manual duplex requires LongEdge or ShortEdge mode.",
                nameof(mode));

        ValidateRotation(longEdgeBackRotationDegrees);
        ValidateRotation(shortEdgeBackRotationDegrees);

        var sheets = new List<DuplexSheet>();
        var sheetIndex = 0;

        for (var copyIndex = 0; copyIndex < copies; copyIndex++)
        {
            for (var outputPage = 0;
                 outputPage < outputPageCount;
                 outputPage += 2)
            {
                sheets.Add(new(
                    SheetIndex: sheetIndex++,
                    CopyIndex: copyIndex,
                    FrontOutputPageIndex: outputPage,
                    BackOutputPageIndex:
                        outputPage + 1 < outputPageCount
                            ? outputPage + 1
                            : null));
            }
        }

        var frontPass = sheets
            .Select(sheet => new PrintSideInstruction(
                sheet.SheetIndex,
                sheet.CopyIndex,
                sheet.FrontOutputPageIndex,
                RotationDegrees: 0))
            .ToArray();

        IEnumerable<DuplexSheet> backSheets =
            sheets.Where(sheet => sheet.BackOutputPageIndex is not null);

        if (backOrder == ManualDuplexBackOrder.Reverse)
            backSheets = backSheets.Reverse();

        var rotation = mode == DuplexMode.LongEdge
            ? longEdgeBackRotationDegrees
            : shortEdgeBackRotationDegrees;

        var backPass = backSheets
            .Select(sheet => new PrintSideInstruction(
                sheet.SheetIndex,
                sheet.CopyIndex,
                sheet.BackOutputPageIndex!.Value,
                rotation))
            .ToArray();

        return new(mode, sheets, frontPass, backPass);
    }

    private static void ValidateRotation(int degrees)
    {
        if (degrees is not (0 or 180))
        {
            throw new ArgumentOutOfRangeException(
                nameof(degrees),
                "Manual duplex currently supports only 0 or 180 degree back-side rotation.");
        }
    }
}
