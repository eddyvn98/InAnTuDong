using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using PrintAI.Workflows;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class VisualGoldenRegressionTests
{
    private const int Dpi = 100;

    [Fact]
    public void CollageGolden_PreservesSourceAssignmentLayeringAndCircleMask()
    {
        using var fixture = VisualFixture.Create();
        var red = fixture.WriteSolid("red.png", SKColors.Red, 420, 280);
        var blue = fixture.WriteSolid("blue.png", SKColors.Blue, 280, 420);
        var green = fixture.WriteSolid("green.png", SKColors.Green, 360, 360);

        var template = CollageTemplateLibrary
            .ThreePhoto4x6Portrait()
            .Single(item => item.Id == "center-circle-two-sides");

        var job = CollageTemplateLibrary.CreateJob(
            template,
            [
                new SourceSpec(red),
                new SourceSpec(blue),
                new SourceSpec(green)
            ],
            [
                new CollageFrameAssignment(0, 2),
                new CollageFrameAssignment(1, 0),
                new CollageFrameAssignment(2, 1)
            ]);

        var png = SourceJobRenderer.RenderMixedA4(job, 0, Dpi);
        using var rendered = SKBitmap.Decode(png);
        Assert.NotNull(rendered);

        var layout = LayoutEngine.Layout(job);
        var circle = layout.Placements.Single(p => p.Index == 0);
        var left = layout.Placements.Single(p => p.Index == 1);
        var right = layout.Placements.Single(p => p.Index == 2);

        AssertColorNear(
            rendered,
            Center(circle),
            SKColors.Green);

        AssertColorNear(
            rendered,
            new(
                circle.XMm + 2,
                circle.YMm + 2),
            SKColors.Red);

        AssertColorNear(
            rendered,
            new(
                left.XMm + 5,
                left.YMm + left.HeightMm / 2),
            SKColors.Red);

        AssertColorNear(
            rendered,
            new(
                right.XMm + right.WidthMm - 5,
                right.YMm + right.HeightMm / 2),
            SKColors.Blue);
    }

    [Fact]
    public void FitGolden_ContainLetterboxesButCoverFillsTarget()
    {
        using var fixture = VisualFixture.Create();
        var source = fixture.WriteSolid(
            "wide.png",
            SKColors.OrangeRed,
            400,
            200);

        var contain = ExactJob(source, FitMode.Contain);
        var cover = ExactJob(source, FitMode.Cover);

        using var containBitmap = SKBitmap.Decode(
            SourceJobRenderer.RenderMixedA4(
                contain,
                outputPageIndex: 0,
                dpi: Dpi));
        using var coverBitmap = SKBitmap.Decode(
            SourceJobRenderer.RenderMixedA4(
                cover,
                outputPageIndex: 0,
                dpi: Dpi));

        Assert.NotNull(containBitmap);
        Assert.NotNull(coverBitmap);

        var placement = LayoutEngine.Layout(contain)
            .Placements
            .Single();

        var nearTop = new MmPoint(
            placement.XMm + placement.WidthMm / 2,
            placement.YMm + 8);

        var center = Center(placement);

        AssertColorNear(
            containBitmap,
            nearTop,
            SKColors.White);

        AssertColorNear(
            containBitmap,
            center,
            SKColors.OrangeRed);

        AssertColorNear(
            coverBitmap,
            nearTop,
            SKColors.OrangeRed);

        AssertColorNear(
            coverBitmap,
            center,
            SKColors.OrangeRed);
    }

    [Fact]
    public void GridGolden_PreservesCopiesGapAndCutMarks()
    {
        using var fixture = VisualFixture.Create();
        var source = fixture.WriteSolid(
            "label.png",
            SKColors.Goldenrod,
            400,
            600);

        var job = LabelSheetWorkflow.CreateJob(
            new SourceSpec(source),
            new LabelSheetOptions(
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                Copies: 24,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: true,
                CutMarks: true,
                Fit: FitMode.Contain));

        using var rendered = SKBitmap.Decode(
            SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: 0,
                dpi: Dpi));

        Assert.NotNull(rendered);

        var layout = LayoutEngine.Layout(job);
        var firstRow = layout.Placements
            .Where(p => p.Page == 0)
            .OrderBy(p => p.YMm)
            .ThenBy(p => p.XMm)
            .Take(layout.Columns)
            .ToArray();

        Assert.True(firstRow.Length >= 2);

        var first = firstRow[0];
        var second = firstRow[1];

        AssertColorNear(
            rendered,
            Center(first),
            SKColors.Goldenrod);

        AssertColorNear(
            rendered,
            new(
                (first.XMm + first.WidthMm + second.XMm) / 2,
                first.YMm + first.HeightMm / 2),
            SKColors.White);

        var mark = CutMarkGenerator.Create(first)[0];
        AssertDarkNearby(
            rendered,
            new(
                (mark.X1Mm + mark.X2Mm) / 2,
                mark.Y1Mm));
    }

    [Fact]
    public void CropPositionGolden_AutoTrimFillsRightAnchoredPlacement()
    {
        using var fixture = VisualFixture.Create();
        var source = fixture.WriteBordered(
            "crop.png",
            200,
            200,
            SKColors.White,
            SKColors.Crimson,
            new SKRect(50, 50, 150, 150));

        var job = new PrintJobSpec(
            "crop-position-golden",
            [new SourceSpec(source)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 100,
                ItemHeightMm: 100,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain,
                PagePlacement: new PagePlacementSpec(
                    new PageMarginsSpec(
                        LeftMm: 20,
                        TopMm: 5,
                        RightMm: 5,
                        BottomMm: 5),
                    Anchor: PageAnchor.Right),
                SourceCrop: new SourceCropSpec(
                    SourceCropMode.AutoTrimWhite)),
            new PrintSettings(),
            new PolicySpec());

        using var rendered = SKBitmap.Decode(
            SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: 0,
                dpi: Dpi));

        Assert.NotNull(rendered);

        var placement = LayoutEngine.Layout(job)
            .Placements
            .Single();

        Assert.True(placement.XMm >= 20);

        AssertColorNear(
            rendered,
            new(
                placement.XMm + 5,
                placement.YMm + placement.HeightMm / 2),
            SKColors.Crimson);

        AssertColorNear(
            rendered,
            new(
                Math.Max(1, placement.XMm - 5),
                placement.YMm + placement.HeightMm / 2),
            SKColors.White);
    }

    private static PrintJobSpec ExactJob(
        string source,
        FitMode fit) =>
        new(
            $"fit-{fit}",
            [new SourceSpec(source)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 100,
                ItemHeightMm: 100,
                MarginMm: 5,
                AllowRotate: false,
                Fit: fit),
            new PrintSettings(),
            new PolicySpec());

    private static MmPoint Center(Placement placement) =>
        new(
            placement.XMm + placement.WidthMm / 2,
            placement.YMm + placement.HeightMm / 2);

    private static void AssertColorNear(
        SKBitmap bitmap,
        MmPoint point,
        SKColor expected,
        byte tolerance = 28)
    {
        var pixel = bitmap.GetPixel(
            MmToPx(point.XMm),
            MmToPx(point.YMm));

        Assert.InRange(
            Math.Abs(pixel.Red - expected.Red),
            0,
            tolerance);
        Assert.InRange(
            Math.Abs(pixel.Green - expected.Green),
            0,
            tolerance);
        Assert.InRange(
            Math.Abs(pixel.Blue - expected.Blue),
            0,
            tolerance);
    }

    private static void AssertDarkNearby(
        SKBitmap bitmap,
        MmPoint point)
    {
        var centerX = MmToPx(point.XMm);
        var centerY = MmToPx(point.YMm);
        var darkest = 255;

        for (var y = Math.Max(0, centerY - 2);
             y <= Math.Min(bitmap.Height - 1, centerY + 2);
             y++)
        {
            for (var x = Math.Max(0, centerX - 2);
                 x <= Math.Min(bitmap.Width - 1, centerX + 2);
                 x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                darkest = Math.Min(
                    darkest,
                    Math.Max(
                        pixel.Red,
                        Math.Max(pixel.Green, pixel.Blue)));
            }
        }

        Assert.True(
            darkest < 180,
            $"Expected a dark cut mark near {point}, darkest channel max was {darkest}.");
    }

    private static int MmToPx(double mm) =>
        Math.Max(
            0,
            (int)Math.Round(mm / 25.4 * Dpi));

    private readonly record struct MmPoint(
        double XMm,
        double YMm);

    private sealed class VisualFixture : IDisposable
    {
        private VisualFixture(string directory)
        {
            Directory = directory;
        }

        public string Directory { get; }

        public static VisualFixture Create()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"printai-visual-{Guid.NewGuid():N}");

            System.IO.Directory.CreateDirectory(directory);
            return new(directory);
        }

        public string WriteSolid(
            string name,
            SKColor color,
            int width,
            int height)
        {
            var path = Path.Combine(Directory, name);

            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(color);
            SavePng(bitmap, path);
            return path;
        }

        public string WriteBordered(
            string name,
            int width,
            int height,
            SKColor background,
            SKColor content,
            SKRect contentRect)
        {
            var path = Path.Combine(Directory, name);

            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(background);

            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint
            {
                Color = content,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawRect(contentRect, paint);
            canvas.Flush();

            SavePng(bitmap, path);
            return path;
        }

        private static void SavePng(
            SKBitmap bitmap,
            string path)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(
                SKEncodedImageFormat.Png,
                100);

            Assert.NotNull(data);
            File.WriteAllBytes(
                path,
                data.ToArray());
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(
                    Directory,
                    recursive: true);
            }
            catch
            {
            }
        }
    }
}
