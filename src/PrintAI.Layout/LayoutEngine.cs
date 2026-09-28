using PrintAI.Domain;

namespace PrintAI.Layout;

public static class LayoutEngine
{
    public static LayoutResult Layout(PrintJobSpec job) =>
        job.Layout.Mode switch
        {
            LayoutMode.Grid => GridLayoutEngine.Layout(job),
            LayoutMode.ExactSize => ExactSizeLayoutEngine.Layout(job),
            LayoutMode.Canvas => CanvasLayoutEngine.Layout(job),
            _ => throw new ArgumentOutOfRangeException(nameof(job.Layout.Mode))
        };
}
