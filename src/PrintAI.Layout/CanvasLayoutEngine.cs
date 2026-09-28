using PrintAI.Domain;

namespace PrintAI.Layout;

public static class CanvasLayoutEngine
{
    public static LayoutResult Layout(PrintJobSpec job)
    {
        if (job.Layout.Mode != LayoutMode.Canvas)
            throw new ArgumentException("CanvasLayoutEngine requires LayoutMode.Canvas.");

        var validation = PrintJobValidator.Validate(job);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors.Select(error => error.Message)));

        var canvas = job.Layout.Canvas
            ?? throw new ArgumentException("Canvas layout is required.");

        var placements = canvas.Placements
            .Select((placement, index) => new Placement(
                Index: index,
                Page: 0,
                XMm: placement.XMm,
                YMm: placement.YMm,
                WidthMm: placement.WidthMm,
                HeightMm: placement.HeightMm,
                Rotated: false,
                SourceIndex: placement.SourceIndex,
                SourceCopyIndex: 0))
            .ToArray();

        return new LayoutResult(
            Columns: 0,
            Rows: 0,
            CapacityPerPage: placements.Length,
            Rotated: false,
            Placements: placements);
    }
}
