using PrintAI.Domain;

namespace PrintAI.Workflows;

public sealed record CollageTemplate(
    string Id,
    string Title,
    IReadOnlyList<string> Tags,
    CanvasLayoutSpec Canvas);

public sealed record CollageFrameAssignment(
    int FrameIndex,
    int SourceIndex,
    double Scale = 1,
    double OffsetX = 0,
    double OffsetY = 0);

public static class CollageTemplateLibrary
{
    public static IReadOnlyList<CollageTemplate> ThreePhoto4x6Portrait() =>
    [
        Template("hero-left-two-right", "Ảnh chính bên trái", ["editorial", "hero"],
            P(0, 4, 4, 59, 144, FrameShape.RoundedRectangle, radius: 4),
            P(1, 67, 8, 30.6, 66, FrameShape.RoundedRectangle, radius: 4),
            P(2, 67, 78, 30.6, 66, FrameShape.RoundedRectangle, radius: 4)),

        Template("hero-top-two-bottom", "Ảnh chính phía trên", ["balanced", "hero"],
            P(0, 4, 4, 93.6, 82, FrameShape.RoundedRectangle, radius: 4),
            P(1, 4, 90, 44.8, 58.4, FrameShape.RoundedRectangle, radius: 4),
            P(2, 52.8, 90, 44.8, 58.4, FrameShape.RoundedRectangle, radius: 4)),

        Template("hero-right-two-left", "Ảnh chính bên phải", ["editorial", "hero"],
            P(0, 38.6, 4, 59, 144, FrameShape.RoundedRectangle, radius: 4),
            P(1, 4, 8, 30.6, 66, FrameShape.RoundedRectangle, radius: 4),
            P(2, 4, 78, 30.6, 66, FrameShape.RoundedRectangle, radius: 4)),

        Template("three-rounded-columns", "Ba cột mềm", ["clean", "balanced"],
            P(0, 4, 14, 29.9, 124, FrameShape.RoundedRectangle, radius: 8),
            P(1, 35.9, 7, 29.9, 138, FrameShape.RoundedRectangle, radius: 8),
            P(2, 67.8, 14, 29.8, 124, FrameShape.RoundedRectangle, radius: 8)),

        Template("center-circle-two-sides", "Tròn trung tâm", ["playful", "shape"],
            P(0, 28.3, 43, 45, 45, FrameShape.Circle, z: 3, scale: 1.12),
            P(1, 4, 18, 39, 116, FrameShape.RoundedRectangle, radius: 6),
            P(2, 58.6, 18, 39, 116, FrameShape.RoundedRectangle, radius: 6)),

        Template("overlapping-cards", "Thẻ ảnh chồng lớp", ["playful", "overlap"],
            P(0, 8, 18, 56, 105, FrameShape.RoundedRectangle, rotation: -7, radius: 5, z: 1),
            P(1, 37, 9, 56, 105, FrameShape.RoundedRectangle, rotation: 7, radius: 5, z: 2),
            P(2, 23, 42, 56, 105, FrameShape.RoundedRectangle, rotation: 0, radius: 5, z: 3)),

        Template("diagonal-cards", "Đường chéo", ["dynamic", "overlap"],
            P(0, 5, 5, 49, 82, FrameShape.RoundedRectangle, rotation: -4, radius: 4, z: 1),
            P(1, 48, 35, 49, 82, FrameShape.RoundedRectangle, rotation: 5, radius: 4, z: 2),
            P(2, 10, 69, 49, 78, FrameShape.RoundedRectangle, rotation: -2, radius: 4, z: 3)),

        Template("large-center-corners", "Ảnh lớn trung tâm", ["focus", "shape"],
            P(0, 20, 26, 61.6, 100, FrameShape.Ellipse, z: 1, scale: 1.08),
            P(1, 4, 5, 34, 45, FrameShape.RoundedRectangle, rotation: -5, radius: 4, z: 2),
            P(2, 63.6, 102, 34, 45, FrameShape.RoundedRectangle, rotation: 5, radius: 4, z: 2)),

        Template("stacked-soft", "Xếp tầng mềm", ["soft", "overlap"],
            P(0, 8, 6, 75, 62, FrameShape.RoundedRectangle, radius: 10, z: 1),
            P(1, 18, 49, 75, 62, FrameShape.RoundedRectangle, radius: 10, z: 2),
            P(2, 8, 92, 75, 56, FrameShape.RoundedRectangle, radius: 10, z: 3)),

        Template("asymmetrical-editorial", "Editorial bất đối xứng", ["editorial", "modern"],
            P(0, 4, 4, 62, 91, FrameShape.RoundedRectangle, radius: 3),
            P(1, 70, 4, 27.6, 91, FrameShape.Ellipse, scale: 1.1),
            P(2, 4, 99, 93.6, 49, FrameShape.RoundedRectangle, radius: 3))
    ];

    public static PrintJobSpec CreateJob(
        CollageTemplate template,
        IReadOnlyList<SourceSpec> sources)
    {
        if (sources.Count != 3)
            throw new ArgumentException("The initial collage template library requires exactly three sources.");

        return CreateJobCore(
            template,
            sources,
            template.Canvas.Placements
                .Select((placement, frameIndex) => new CollageFrameAssignment(
                    FrameIndex: frameIndex,
                    SourceIndex: placement.SourceIndex,
                    Scale: 1,
                    OffsetX: 0,
                    OffsetY: 0))
                .ToArray(),
            preserveTemplateTransform: true);
    }

    public static PrintJobSpec CreateJob(
        CollageTemplate template,
        IReadOnlyList<SourceSpec> sources,
        IReadOnlyList<CollageFrameAssignment> assignments) =>
        CreateJobCore(
            template,
            sources,
            assignments,
            preserveTemplateTransform: false);

    private static PrintJobSpec CreateJobCore(
        CollageTemplate template,
        IReadOnlyList<SourceSpec> sources,
        IReadOnlyList<CollageFrameAssignment> assignments,
        bool preserveTemplateTransform)
    {
        if (sources.Count != 3)
            throw new ArgumentException("The initial collage template library requires exactly three sources.");

        if (assignments.Count != template.Canvas.Placements.Count)
            throw new ArgumentException("Every collage frame requires exactly one assignment.");

        if (!assignments.Select(item => item.FrameIndex).Order().SequenceEqual(
                Enumerable.Range(0, template.Canvas.Placements.Count)))
        {
            throw new ArgumentException("Frame assignments must cover every template frame exactly once.");
        }

        if (!assignments.Select(item => item.SourceIndex).Order().SequenceEqual([0, 1, 2]))
            throw new ArgumentException("Each collage source must be used exactly once.");

        var byFrame = assignments.ToDictionary(item => item.FrameIndex);

        var placements = template.Canvas.Placements
            .Select((placement, frameIndex) =>
            {
                var assignment = byFrame[frameIndex];
                var baseTransform = placement.Transform ?? new ImageTransformSpec();

                var transform = preserveTemplateTransform
                    ? baseTransform
                    : new ImageTransformSpec(
                        Scale: Math.Clamp(
                            baseTransform.Scale * assignment.Scale,
                            1,
                            10),
                        OffsetX: Math.Clamp(
                            baseTransform.OffsetX + assignment.OffsetX,
                            -1,
                            1),
                        OffsetY: Math.Clamp(
                            baseTransform.OffsetY + assignment.OffsetY,
                            -1,
                            1));

                return placement with
                {
                    SourceIndex = assignment.SourceIndex,
                    Transform = transform
                };
            })
            .ToArray();

        return new PrintJobSpec(
            JobName: $"Smart collage - {template.Title}",
            Sources: sources,
            Paper: new PaperSpec(
                AutoLayoutWorkflow.FourBySixWidthMm,
                AutoLayoutWorkflow.FourBySixHeightMm,
                PageOrientation.Portrait),
            Layout: new LayoutSpec(
                LayoutMode.Canvas,
                ItemWidthMm: 1,
                ItemHeightMm: 1,
                Fit: FitMode.Cover,
                Canvas: new CanvasLayoutSpec(placements)),
            Print: new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.High),
            Policy: new PolicySpec(PreviewPolicy.Required));
    }

    private static CollageTemplate Template(
        string id,
        string title,
        IReadOnlyList<string> tags,
        params CanvasPlacementSpec[] placements) =>
        new(id, title, tags, new CanvasLayoutSpec(placements));

    private static CanvasPlacementSpec P(
        int source,
        double x,
        double y,
        double width,
        double height,
        FrameShape shape,
        double rotation = 0,
        double radius = 0,
        int z = 0,
        double scale = 1) =>
        new(
            SourceIndex: source,
            XMm: x,
            YMm: y,
            WidthMm: width,
            HeightMm: height,
            RotationDegrees: rotation,
            ZIndex: z,
            Shape: new ShapeSpec(shape, radius),
            Transform: new ImageTransformSpec(scale),
            Fit: FitMode.Cover);
}
