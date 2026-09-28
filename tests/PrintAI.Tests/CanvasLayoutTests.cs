using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Workflows;
using Xunit;

namespace PrintAI.Tests;

public sealed class CanvasLayoutTests
{
    [Fact]
    public void ThreePhotoTemplates_ExposeTenDistinctDesigns()
    {
        var templates = CollageTemplateLibrary.ThreePhoto4x6Portrait();

        Assert.True(templates.Count >= 10);
        Assert.Equal(
            templates.Count,
            templates.Select(template => template.Id).Distinct().Count());
    }

    [Fact]
    public void EveryInitialTemplate_ValidatesAndStaysInside4x6()
    {
        var sources = new[]
        {
            new SourceSpec("a.png"),
            new SourceSpec("b.png"),
            new SourceSpec("c.png")
        };

        foreach (var template in CollageTemplateLibrary.ThreePhoto4x6Portrait())
        {
            var job = CollageTemplateLibrary.CreateJob(template, sources);
            var validation = PrintJobValidator.Validate(job);
            Assert.True(
                validation.IsValid,
                $"{template.Id}: {string.Join("; ", validation.Errors.Select(error => error.Message))}");

            var layout = LayoutEngine.Layout(job);
            Assert.Equal(3, layout.Placements.Count);
            Assert.Equal(LayoutMode.Canvas, job.Layout.Mode);
        }
    }

    [Fact]
    public void AiAssignments_PermuteSourcesAndLayerOnTemplateTransform()
    {
        var template = CollageTemplateLibrary.ThreePhoto4x6Portrait()
            .Single(item => item.Id == "center-circle-two-sides");

        var job = CollageTemplateLibrary.CreateJob(
            template,
            [
                new SourceSpec("a.png"),
                new SourceSpec("b.png"),
                new SourceSpec("c.png")
            ],
            [
                new CollageFrameAssignment(0, 2, Scale: 1.2, OffsetX: 0.1, OffsetY: -0.1),
                new CollageFrameAssignment(1, 0),
                new CollageFrameAssignment(2, 1)
            ]);

        var placements = job.Layout.Canvas!.Placements;

        Assert.Equal(2, placements[0].SourceIndex);
        Assert.Equal(0, placements[1].SourceIndex);
        Assert.Equal(1, placements[2].SourceIndex);
        Assert.True(placements[0].Transform!.Scale > 1.2);
        Assert.Equal(0.1, placements[0].Transform!.OffsetX, 3);
        Assert.Equal(-0.1, placements[0].Transform!.OffsetY, 3);
    }

    [Fact]
    public void CanvasRejectsPlacementOutsidePaper()
    {
        var job = new PrintJobSpec(
            "bad canvas",
            [new SourceSpec("a.png")],
            new PaperSpec(101.6, 152.4),
            new LayoutSpec(
                LayoutMode.Canvas,
                1,
                1,
                Canvas: new CanvasLayoutSpec(
                [
                    new CanvasPlacementSpec(
                        0,
                        XMm: 90,
                        YMm: 10,
                        WidthMm: 20,
                        HeightMm: 20)
                ])),
            new PrintSettings(),
            new PolicySpec());

        var validation = PrintJobValidator.Validate(job);

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            error => error.Code == "layout.canvas.bounds");
    }

    [Fact]
    public void CanvasRejectsUnavailableSource()
    {
        var job = new PrintJobSpec(
            "bad source",
            [new SourceSpec("a.png")],
            new PaperSpec(101.6, 152.4),
            new LayoutSpec(
                LayoutMode.Canvas,
                1,
                1,
                Canvas: new CanvasLayoutSpec(
                [
                    new CanvasPlacementSpec(
                        2,
                        XMm: 4,
                        YMm: 4,
                        WidthMm: 20,
                        HeightMm: 20)
                ])),
            new PrintSettings(),
            new PolicySpec());

        var validation = PrintJobValidator.Validate(job);

        Assert.Contains(
            validation.Errors,
            error => error.Code == "layout.canvas.source");
    }

    [Fact]
    public void TemplateLibrary_ContainsMasksTransformsAndOverlap()
    {
        var templates = CollageTemplateLibrary.ThreePhoto4x6Portrait();
        var placements = templates.SelectMany(template => template.Canvas.Placements).ToArray();

        Assert.Contains(placements, p => p.Shape?.Kind == FrameShape.Circle);
        Assert.Contains(placements, p => p.Shape?.Kind == FrameShape.Ellipse);
        Assert.Contains(placements, p => p.Shape?.Kind == FrameShape.RoundedRectangle);
        Assert.Contains(placements, p => Math.Abs(p.RotationDegrees) > 0.01);
        Assert.Contains(placements, p => (p.Transform?.Scale ?? 1) > 1);
        Assert.Contains(placements, p => p.ZIndex > 0);
    }
}
