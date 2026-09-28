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
