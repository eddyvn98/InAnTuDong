using PrintAI.Domain;
using PrintAI.Rendering;
using PrintAI.Scanning;
using PrintAI.SourceInspection;
using PrintAI.Workflows;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class ReleasePipelineRegressionTests
{
    [Fact]
    public void RasterSource_InspectsAndRendersToA4()
    {
        using var fixture = ReleaseFixture.Create();
        var imagePath = fixture.WriteTinyPng("source.png");

        var metadata = SourceInspector.Inspect(imagePath);
        var job = CreateExactJob(imagePath);

        var output = SourceJobRenderer.RenderA4(
            job,
            imagePath,
            sourcePageIndex: 0,
            outputPageIndex: 0,
            dpi: 96);

        Assert.Equal(SourceKind.Png, metadata.Kind);
        Assert.Equal(32, metadata.PixelWidth);
        Assert.Equal(32, metadata.PixelHeight);
        AssertPng(output);
    }

    [Fact]
    public void MultiPagePdf_InspectsAndRendersSecondPage()
    {
        using var fixture = ReleaseFixture.Create();
        var imagePath = fixture.WriteTinyPng("scan.png");
        var pdfPath = fixture.PathFor("two-pages.pdf");

        ScanPdfWriter.Write(
            [imagePath, imagePath],
            pdfPath);

        var metadata = SourceInspector.Inspect(pdfPath);
        var job = CreateExactJob(
            pdfPath,
            pageIndex: 1);

        var output = SourceJobRenderer.RenderA4(
            job,
            pdfPath,
            sourcePageIndex: 1,
            outputPageIndex: 0,
            dpi: 96);

        Assert.Equal(SourceKind.Pdf, metadata.Kind);
        Assert.Equal(2, metadata.PageCount);
        AssertPng(output);
    }

    [Fact]
    public void MixedImageAndPdfPage_RenderThroughOneJob()
    {
        using var fixture = ReleaseFixture.Create();
        var imagePath = fixture.WriteTinyPng("image.png");
        var pdfPath = fixture.PathFor("document.pdf");

        ScanPdfWriter.Write(
            [imagePath, imagePath],
            pdfPath);

        var job = MixedCompositionWorkflow.CreateJob(
            [
                new SourceSpec(
                    imagePath,
                    Copies: 2),
                new SourceSpec(
                    pdfPath,
                    Copies: 1,
                    PageIndex: 1)
            ],
            new MixedCompositionOptions(
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: true,
                CutMarks: true,
                Fit: FitMode.Contain));

        var output = SourceJobRenderer.RenderMixedA4(
            job,
            outputPageIndex: 0,
            dpi: 96);

        Assert.Equal(1, SourceJobRenderer.GetOutputPageCount(job));
        AssertPng(output);
    }

    private static PrintJobSpec CreateExactJob(
        string path,
        int pageIndex = 0) =>
        new(
            "release-regression",
            [new SourceSpec(path, PageIndex: pageIndex)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 20,
                ItemHeightMm: 20,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            new PrintSettings(),
            new PolicySpec(PreviewPolicy.Required));

    private static void AssertPng(byte[] bytes)
    {
        Assert.True(bytes.Length > 100);
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            bytes.Take(8).ToArray());
    }

    private sealed class ReleaseFixture : IDisposable
    {
        private ReleaseFixture(string directory)
        {
            Directory = directory;
        }

        public string Directory { get; }

        public static ReleaseFixture Create()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"printai-release-test-{Guid.NewGuid():N}");

            System.IO.Directory.CreateDirectory(directory);
            return new(directory);
        }

        public string WriteTinyPng(string name)
        {
            var path = PathFor(name);

            using var bitmap = new SKBitmap(32, 32);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawRect(8, 8, 16, 16, paint);
            canvas.Flush();

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(
                SKEncodedImageFormat.Png,
                100);

            if (data is null)
                throw new InvalidOperationException("Could not encode regression PNG.");

            File.WriteAllBytes(path, data.ToArray());
            return path;
        }

        public string PathFor(string name) =>
            Path.Combine(Directory, name);

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
