using PrintAI.Domain;
using PrintAI.Rendering;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class SourceJobRendererTests
{
    [Fact]
    public void TwentyFourBySixCmCopies_ExposeEveryOutputPage()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(120, 180))
            {
                bitmap.Erase(SKColors.DarkOrange);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(
                    SKEncodedImageFormat.Png,
                    100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "20 copies",
                [new SourceSpec(path, Copies: 20)],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.Grid,
                    40,
                    60,
                    GapMm: 3,
                    MarginMm: 5,
                    AllowRotate: true,
                    CutMarks: true,
                    Fit: FitMode.Cover),
                new PrintSettings(),
                new PolicySpec());

            Assert.Equal(2, SourceJobRenderer.GetOutputPageCount(job));

            var first = SourceJobRenderer.RenderA4(
                job, path, 0, 0, dpi: 72);
            var second = SourceJobRenderer.RenderA4(
                job, path, 0, 1, dpi: 72);

            Assert.NotEmpty(first);
            Assert.NotEmpty(second);
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Fact]
    public void SourceThumbnail_IsDownscaledForVision()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(1200, 600))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var png = SourceJobRenderer.RenderSourceThumbnailPng(
                path,
                sourcePageIndex: 0,
                maxDimension: 256);

            using var decoded = SKBitmap.Decode(png);
            Assert.NotNull(decoded);
            Assert.Equal(256, decoded.Width);
            Assert.Equal(128, decoded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void GridItemBorder_DrawsVisibleRectangle()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(100, 100))
            {
                bitmap.Erase(SKColors.White);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "border",
                [new SourceSpec(path, Copies: 4)],
                new PaperSpec(),
                new LayoutSpec(
                    LayoutMode.Grid,
                    ItemWidthMm: 99,
                    ItemHeightMm: 141.5,
                    GapMm: 2,
                    MarginMm: 5,
                    AllowRotate: false,
                    Fit: FitMode.Contain,
                    ItemBorder: true),
                new PrintSettings(),
                new PolicySpec());

            var png = SourceJobRenderer.RenderA4(
                job,
                path,
                sourcePageIndex: 0,
                outputPageIndex: 0,
                dpi: 100);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            var edgeX = Px(50);
            var edgeY = Px(5);
            var edgeNeighborhood = new List<SKColor>();

            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    edgeNeighborhood.Add(
                        rendered.GetPixel(edgeX + dx, edgeY + dy));
                }
            }

            var center = rendered.GetPixel(Px(50), Px(50));

            Assert.Contains(
                edgeNeighborhood,
                pixel =>
                    pixel.Red < 220 &&
                    pixel.Green < 220 &&
                    pixel.Blue < 220);

            Assert.True(
                center.Red > 240 &&
                center.Green > 240 &&
                center.Blue > 240);
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void CanvasCircleMask_ClipsBoundingBoxCorners()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            using (var bitmap = new SKBitmap(200, 200))
            {
                bitmap.Erase(SKColors.Red);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(path);
                data.SaveTo(stream);
            }

            var job = new PrintJobSpec(
                "circle mask",
                [new SourceSpec(path)],
                new PaperSpec(101.6, 152.4),
                new LayoutSpec(
                    LayoutMode.Canvas,
                    ItemWidthMm: 1,
                    ItemHeightMm: 1,
                    Canvas: new CanvasLayoutSpec(
                    [
                        new CanvasPlacementSpec(
                            SourceIndex: 0,
                            XMm: 10,
                            YMm: 10,
                            WidthMm: 50,
                            HeightMm: 50,
                            Shape: new ShapeSpec(FrameShape.Circle),
                            Fit: FitMode.Cover)
                    ])),
                new PrintSettings(),
                new PolicySpec());

            var png = SourceJobRenderer.RenderA4(
                job,
                path,
                sourcePageIndex: 0,
                outputPageIndex: 0,
                dpi: 100);

            using var rendered = SKBitmap.Decode(png);
            Assert.NotNull(rendered);

            static int Px(double mm) =>
                (int)Math.Round(mm / 25.4 * 100);

            var corner = rendered.GetPixel(Px(12), Px(12));
            var center = rendered.GetPixel(Px(35), Px(35));

            Assert.True(corner.Red > 240 && corner.Green > 240 && corner.Blue > 240);
            Assert.True(center.Red > 240 && center.Green < 30 && center.Blue < 30);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SourceThumbnail_HonorsExifOrientation()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.jpg");

        try
        {
            using var bitmap = new SKBitmap(40, 20);
            bitmap.Erase(SKColors.CadetBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            Assert.NotNull(data);

            var encoded = data.ToArray();
            var exif = CreateExifOrientationSegment(6);
            var jpeg = new byte[encoded.Length + exif.Length];
            Buffer.BlockCopy(encoded, 0, jpeg, 0, 2);
            Buffer.BlockCopy(exif, 0, jpeg, 2, exif.Length);
            Buffer.BlockCopy(
                encoded,
                2,
                jpeg,
                2 + exif.Length,
                encoded.Length - 2);
            File.WriteAllBytes(path, jpeg);

            var png = SourceJobRenderer.RenderSourceThumbnailPng(
                path,
                sourcePageIndex: 0,
                maxDimension: 256);

            using var decoded = SKBitmap.Decode(png);
            Assert.NotNull(decoded);
            Assert.Equal(20, decoded.Width);
            Assert.Equal(40, decoded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreateExifOrientationSegment(ushort orientation)
    {
        var payload = new byte[]
        {
            0x45,0x78,0x69,0x66,0x00,0x00,
            0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00,
            0x01,0x00,
            0x12,0x01,0x03,0x00,0x01,0x00,0x00,0x00,
            (byte)orientation,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00
        };

        var length = payload.Length + 2;
        var segment = new byte[payload.Length + 4];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        segment[2] = (byte)(length >> 8);
        segment[3] = (byte)length;
        Buffer.BlockCopy(payload, 0, segment, 4, payload.Length);
        return segment;
    }


}
