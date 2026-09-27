using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using PrintAI.Workflows;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class MixedJobTests
{
    [Fact]
    public void CccdFrontBack_MapsSourcesInFrontThenBackOrder()
    {
        var job = CccdWorkflow.CreateFrontBackJob(
            new SourceSpec("front.png"),
            new SourceSpec("back.png"));

        var layout = LayoutEngine.Layout(job);

        Assert.Equal(2, job.Sources.Count);
        Assert.Equal(2, layout.Placements.Count);
        Assert.Equal(0, layout.Placements[0].SourceIndex);
        Assert.Equal(1, layout.Placements[1].SourceIndex);
        Assert.Equal(0, layout.Placements[0].Page);
        Assert.Equal(0, layout.Placements[1].Page);
        Assert.True(layout.Placements[1].XMm > layout.Placements[0].XMm);
    }

    [Fact]
    public void CccdFrontBack_PreservesPdfPageIndexes()
    {
        var job = CccdWorkflow.CreateFrontBackJob(
            new SourceSpec("scan.pdf", PageIndex: 2),
            new SourceSpec("scan.pdf", PageIndex: 3));

        Assert.Equal(2, job.Sources[0].PageIndex);
        Assert.Equal(3, job.Sources[1].PageIndex);
        Assert.Equal(85.60, job.Layout.ItemWidthMm, 2);
        Assert.Equal(53.98, job.Layout.ItemHeightMm, 2);
        Assert.Equal(FitMode.Contain, job.Layout.Fit);
        Assert.Equal(PreviewPolicy.Required, job.Policy.Preview);
    }

    [Fact]
    public void Validator_RejectsNegativeSourcePageIndex()
    {
        var job = CccdWorkflow.CreateFrontBackJob(
            new SourceSpec("front.pdf", PageIndex: -1),
            new SourceSpec("back.pdf", PageIndex: 0));

        var result = PrintJobValidator.Validate(job);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == "sources.pageIndex");
    }

    [Fact]
    public void MixedRenderer_DrawsDifferentSourcesIntoTheirPlacements()
    {
        var frontPath = TempPng(SKColors.Red);
        var backPath = TempPng(SKColors.Blue);

        try
        {
            var job = CccdWorkflow.CreateFrontBackJob(
                new SourceSpec(frontPath),
                new SourceSpec(backPath));

            var layout = LayoutEngine.Layout(job);
            var png = SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: 0,
                dpi: 96);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);

            var front = layout.Placements[0];
            var back = layout.Placements[1];

            var frontPixel = rendered.GetPixel(
                MmToPx(front.XMm + front.WidthMm / 2, 96),
                MmToPx(front.YMm + front.HeightMm / 2, 96));

            var backPixel = rendered.GetPixel(
                MmToPx(back.XMm + back.WidthMm / 2, 96),
                MmToPx(back.YMm + back.HeightMm / 2, 96));

            Assert.True(frontPixel.Red > 220);
            Assert.True(frontPixel.Blue < 40);
            Assert.True(backPixel.Blue > 220);
            Assert.True(backPixel.Red < 40);
        }
        finally
        {
            File.Delete(frontPath);
            File.Delete(backPath);
        }
    }

    private static string TempPng(SKColor color)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        using var bitmap = new SKBitmap(240, 150);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());

        return path;
    }

    private static int MmToPx(double mm, int dpi) =>
        (int)Math.Round(mm / 25.4 * dpi);
}
