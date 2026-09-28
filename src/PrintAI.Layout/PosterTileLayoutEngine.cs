using PrintAI.Domain;

namespace PrintAI.Layout;

public static class PosterTileLayoutEngine
{
    public static LayoutResult Layout(PrintJobSpec job)
    {
        if (job.Layout.Mode != LayoutMode.PosterTile)
        {
            throw new ArgumentException(
                "PosterTileLayoutEngine requires LayoutMode.PosterTile.");
        }

        var validation = PrintJobValidator.Validate(job);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                string.Join(
                    "; ",
                    validation.Errors.Select(error => error.Message)));
        }

        var paperWidth =
            job.Paper.Orientation == PageOrientation.Portrait
                ? job.Paper.WidthMm
                : job.Paper.HeightMm;
        var paperHeight =
            job.Paper.Orientation == PageOrientation.Portrait
                ? job.Paper.HeightMm
                : job.Paper.WidthMm;

        var placements = new List<Placement>();
        var page = 0;

        for (var sourceIndex = 0;
             sourceIndex < job.Sources.Count;
             sourceIndex++)
        {
            var source = job.Sources[sourceIndex];
            var tile = source.PosterTile
                ?? throw new ArgumentException(
                    "PosterTile layout requires poster metadata on every source.");

            for (var copy = 0;
                 copy < source.Copies;
                 copy++)
            {
                if (tile.CanvasWidthMm +
                        (2 * job.Layout.MarginMm) >
                        paperWidth + 0.001 ||
                    tile.CanvasHeightMm +
                        (2 * job.Layout.MarginMm) >
                        paperHeight + 0.001)
                {
                    throw new InvalidOperationException(
                        "Poster tile does not fit inside the physical paper.");
                }

                placements.Add(new(
                    Index: placements.Count,
                    Page: page++,
                    XMm: job.Layout.MarginMm,
                    YMm: job.Layout.MarginMm,
                    WidthMm: tile.CanvasWidthMm,
                    HeightMm: tile.CanvasHeightMm,
                    Rotated: false,
                    SourceIndex: sourceIndex,
                    SourceCopyIndex: copy));
            }
        }

        return new(
            Columns: 1,
            Rows: 1,
            CapacityPerPage: 1,
            Rotated: false,
            Placements: placements);
    }
}
