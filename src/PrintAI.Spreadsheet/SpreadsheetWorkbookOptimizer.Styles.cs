using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PrintAI.Spreadsheet;

public static partial class SpreadsheetWorkbookOptimizer
{
    private static void ApplyReadableStyles(
        WorkbookPart workbookPart,
        SheetData sheetData,
        SpreadsheetSheetPrintPlan plan)
    {
        var styles = workbookPart.WorkbookStylesPart?.Stylesheet
            ?? throw new InvalidDataException(
                "Workbook styles are missing.");
        var cellFormats = styles.CellFormats!;
        var fonts = styles.Fonts!;

        var usedStyleIndexes = sheetData
            .Descendants<Cell>()
            .Select(cell =>
                (int)(cell.StyleIndex?.Value ?? 0))
            .Distinct()
            .ToArray();

        var styleMap =
            new Dictionary<int, uint>();

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

            var newFont =
                (Font)originalFont.CloneNode(true);

            var existingSize =
                newFont.FontSize?.Val?.Value ??
                11d;

            if (existingSize < plan.MinimumFontPt)
            {
                newFont.FontSize =
                    new FontSize
                    {
                        Val = plan.MinimumFontPt
                    };
            }

            fonts.Append(newFont);
            var newFontId =
                (uint)(fonts.ChildElements.Count - 1);

            var newFormat =
                (CellFormat)original.CloneNode(true);

            newFormat.FontId = newFontId;
            newFormat.ApplyFont = true;

            if (plan.WrapText)
            {
                var alignment =
                    newFormat.Alignment is null
                        ? new Alignment()
                        : (Alignment)newFormat
                            .Alignment
                            .CloneNode(true);

                alignment.WrapText = true;
                alignment.Vertical =
                    VerticalAlignmentValues.Top;
                newFormat.Alignment = alignment;
                newFormat.ApplyAlignment = true;
            }

            cellFormats.Append(newFormat);
            styleMap[originalIndex] =
                (uint)(
                    cellFormats.ChildElements.Count -
                    1);
        }

        fonts.Count =
            (uint)fonts.ChildElements.Count;
        cellFormats.Count =
            (uint)cellFormats.ChildElements.Count;

        foreach (var cell in sheetData
                     .Descendants<Cell>())
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

    private static void EnsureStyles(
        WorkbookPart workbookPart)
    {
        var part =
            workbookPart.WorkbookStylesPart ??
            workbookPart
                .AddNewPart<WorkbookStylesPart>();

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
            (uint)part.Stylesheet
                .Fonts.ChildElements.Count;
        part.Stylesheet.Fills.Count =
            (uint)part.Stylesheet
                .Fills.ChildElements.Count;
        part.Stylesheet.Borders.Count =
            (uint)part.Stylesheet
                .Borders.ChildElements.Count;
        part.Stylesheet.CellStyleFormats.Count =
            (uint)part.Stylesheet
                .CellStyleFormats
                .ChildElements.Count;
        part.Stylesheet.CellFormats.Count =
            (uint)part.Stylesheet
                .CellFormats.ChildElements.Count;
        part.Stylesheet.CellStyles.Count =
            (uint)part.Stylesheet
                .CellStyles.ChildElements.Count;

        part.Stylesheet.Save();
    }
}
