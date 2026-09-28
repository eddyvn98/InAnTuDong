using PrintAI.Domain;

namespace PrintAI.Desktop;

public sealed record DesktopJobEdits(
    LayoutMode Mode,
    double ItemWidthMm,
    double ItemHeightMm,
    double GapMm,
    double MarginMm,
    int Copies,
    DuplexMode Duplex,
    bool AllowRotate,
    bool CutMarks,
    FitMode Fit);

public static class DesktopJobEditor
{
    public static PrintJobSpec Apply(
        PrintJobSpec current,
        DesktopJobEdits edits)
    {
        ArgumentNullException.ThrowIfNull(current);

        var sources = current.Sources.Count > 1
            ? current.Sources.ToArray()
            : current.Sources
                .Select(source => source with { Copies = edits.Copies })
                .ToArray();

        var updated = current with
        {
            Sources = sources,
            Print = current.Print with
            {
                Duplex = edits.Duplex
            },
            Layout = current.Layout with
            {
                Mode = edits.Mode,
                ItemWidthMm = edits.ItemWidthMm,
                ItemHeightMm = edits.ItemHeightMm,
                GapMm = edits.GapMm,
                MarginMm = edits.MarginMm,
                AllowRotate = edits.AllowRotate,
                CutMarks = edits.CutMarks,
                Fit = edits.Fit
            }
        };

        var validation = PrintJobValidator.Validate(updated);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                string.Join(
                    "; ",
                    validation.Errors.Select(e => $"{e.Code}: {e.Message}")));
        }

        return updated;
    }
}
