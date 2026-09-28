using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class PosterTileRendererTests
{
    [Fact]
    public void TwoTiles_RenderDifferentRegionsOfTheSameSource()
    {
        var path = CreateHalfRedHalfBlueImage();

        try
        {
            var job = CreatePosterJob(
                path,
                new PosterSpec(
                    TargetWidthMm: 400,
                    TargetHeightMm: 200,
                    OverlapMm: 0,
                    AutoOrientation: false));

            Assert.Equal(2, SourceJobRenderer.GetOutputPageCount(job));

            var first = SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: 0,
                dpi: 100);
            var second = SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: 1,
                dpi: 100);

            using var firstBitmap = SKBitmap.Decode(first);
            using var secondBitmap = SKBitmap.Decode(second);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            var firstCenter = firstBitmap.GetPixel(
                Px(105),
                Px(105));
            var secondCenter = secondBitmap.GetPixel(
                Px(105),
                Px(105));

            Assert.True(
                firstCenter.Red > 220 &&
                firstCenter.Blue < 40);
            Assert.True(
                secondCenter.Blue > 220 &&
                secondCenter.Red < 40);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Contain_PreservesLetterboxOnPosterCanvas()
    {
        var path = CreateSolidImage(
            200,
            100,
            SKColors.Red);

        try
        {
            var job = CreatePosterJob(
                path,
                new PosterSpec(
                    TargetWidthMm: 200,
                    TargetHeightMm: 200,
                    OverlapMm: 0,
                    AutoOrientation: false,
                    Fit: FitMode.Contain));

            var png = SourceJobRenderer.RenderMixedA4(
                job,
                0,
                dpi: 100);

            using var rendered = SKBitmap.Decode(png);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            var top = rendered.GetPixel(
                Px(105),
                Px(30));
            var center = rendered.GetPixel(
                Px(105),
                Px(105));

            Assert.True(
                top.Red > 240 &&
                top.Green > 240 &&
                top.Blue > 240);
            Assert.True(
                center.Red > 220 &&
                center.Green < 40 &&
                center.Blue < 40);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RegistrationMarksAndLabel_DrawBlackGuidanceOnWhiteSource()
    {
        var path = CreateSolidImage(
            400,
            200,
            SKColors.White);

        try
        {
            var job = CreatePosterJob(
                path,
                new PosterSpec(
                    TargetWidthMm: 400,
                    TargetHeightMm: 200,
                    OverlapMm: 0,
                    RegistrationMarks: true,
                    TileLabels: true,
                    AutoOrientation: false));

            var png = SourceJobRenderer.RenderMixedA4(
                job,
                0,
                dpi: 100);

            using var rendered = SKBitmap.Decode(png);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            var mark = rendered.GetPixel(
                Px(203.5),
                Px(105));

            Assert.True(
                mark.Red < 100 &&
                mark.Green < 100 &&
                mark.Blue < 100);

            var foundLabelPixel = false;
            for (var y = Px(1); y <= Px(5); y++)
            {
                for (var x = Px(1); x <= Px(55); x++)
                {
                    var pixel = rendered.GetPixel(x, y);
                    if (pixel.Red < 180 ||
                        pixel.Green < 180 ||
                        pixel.Blue < 180)
                    {
                        foundLabelPixel = true;
                        break;
                    }
                }

                if (foundLabelPixel)
                    break;
            }

            Assert.True(foundLabelPixel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PrintJobSpec CreatePosterJob(
        string path,
        PosterSpec poster)
    {
        var plan = new PrintPlan(
            "poster",
            [
                new PlanSourceSpec(
                    path,
                    1,
                    PixelWidth: 400,
                    PixelHeight: 200)
            ],
            [
                new PrintOutputGroupSpec(
                    "Poster",
                    [new PageSelectionSpec(
                        0,
                        [new PageRangeSpec(1, 1)])],
                    new PaperSpec(),
                    new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287),
                    new OutputPrintSettings(),
                    Poster: poster)
            ],
            new PolicySpec());

        return Assert.Single(
            PrintPlanCompiler.Compile(plan).Batches).Job;
    }

    private static string CreateHalfRedHalfBlueImage()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        using var bitmap = new SKBitmap(400, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);

        using var paint = new SKPaint
        {
            Color = SKColors.Blue
        };

        canvas.DrawRect(
            new SKRect(200, 0, 400, 200),
            paint);

        Save(bitmap, path);
        return path;
    }

    private static string CreateSolidImage(
        int width,
        int height,
        SKColor color)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        Save(bitmap, path);
        return path;
    }

    private static void Save(
        SKBitmap bitmap,
        string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
