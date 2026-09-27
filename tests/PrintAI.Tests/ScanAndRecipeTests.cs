using PrintAI.Domain;
using PrintAI.Recipes;
using PrintAI.Scanning;
using PrintAI.SourceInspection;
using SkiaSharp;
using Xunit;

namespace PrintAI.Tests;

public sealed class ScanAndRecipeTests
{
    [Fact]
    public void ScanImageProcessor_AutoCropsWhiteBed()
    {
        var input = Temp(".png");
        var output = Temp(".png");

        try
        {
            using (var bitmap = new SKBitmap(400, 300))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Black };
                canvas.DrawRect(new SKRect(80, 60, 320, 240), paint);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(input, data.ToArray());
            }

            var result = ScanImageProcessor.Process(
                input,
                output,
                new ScanProcessingSettings(
                    AutoCrop: true,
                    AutoDeskew: false));

            Assert.True(result.Cropped);
            Assert.True(result.PixelWidth < 400);
            Assert.True(result.PixelHeight < 300);
            Assert.True(File.Exists(output));
        }
        finally
        {
            Delete(input);
            Delete(output);
        }
    }

    [Fact]
    public void ScanPdfWriter_CreatesInspectableA4Pdf()
    {
        var imagePath = Temp(".png");
        var pdfPath = Temp(".pdf");

        try
        {
            using (var bitmap = new SKBitmap(200, 300))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(imagePath, data.ToArray());
            }

            ScanPdfWriter.Write([imagePath], pdfPath);
            var metadata = SourceInspector.Inspect(pdfPath);

            Assert.Equal(SourceKind.Pdf, metadata.Kind);
            Assert.Equal(1, metadata.PageCount);
        }
        finally
        {
            Delete(imagePath);
            Delete(pdfPath);
        }
    }

    [Fact]
    public void RecipeStore_RoundTripsReusablePrintSettings()
    {
        var path = Temp(".json");

        try
        {
            var store = new PrintRecipeStore(path);
            var saved = store.Save(new PrintRecipe(
                "",
                "4x6",
                new LayoutSpec(
                    LayoutMode.Grid,
                    40,
                    60,
                    GapMm: 2,
                    CutMarks: true),
                new PrintSettings(),
                new PolicySpec(PreviewPolicy.Direct),
                SourceCopies: 6,
                DirectPrintEligible: true));

            var loaded = Assert.Single(store.Read());
            var job = loaded.CreateJob("C:/scan.png");

            Assert.Equal(saved.Id, loaded.Id);
            Assert.Equal("4x6", loaded.Name);
            Assert.Equal(6, job.Sources[0].Copies);
            Assert.Equal(40, job.Layout.ItemWidthMm);
            Assert.True(job.Layout.CutMarks);
            Assert.True(loaded.DirectPrintEligible);
        }
        finally
        {
            Delete(path);
        }
    }

    private static string Temp(string extension) =>
        Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}{extension}");

    private static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        var temp = path + ".tmp";
        if (File.Exists(temp))
            File.Delete(temp);
    }
}
