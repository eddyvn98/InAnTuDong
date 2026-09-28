using System.IO;
using PrintAI.Domain;
using PrintAI.Rendering;
using PrintAI.Scanning;
using PrintAI.SourceInspection;
using PrintAI.Workflows;
using SkiaSharp;

namespace PrintAI.Desktop;

internal static partial class DesktopSelfTest
{
    private static void CheckPdfRegressionSuite(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string? requestedOutputPath)
    {
        var outputDirectory = string.IsNullOrWhiteSpace(requestedOutputPath)
            ? tempDirectory
            : Path.GetDirectoryName(Path.GetFullPath(requestedOutputPath))
                ?? tempDirectory;

        Directory.CreateDirectory(outputDirectory);

        CheckExifPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-exif.pdf"));

        CheckMixedPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-mixed.pdf"));

        CheckNUpPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-nup.pdf"));

        CheckBookletPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-booklet.pdf"));

        CheckPosterPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-poster.pdf"));

        CheckCollagePdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-collage.pdf"));

        CheckAutoLayoutPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-auto-layout.pdf"));

        CheckGridLabelPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-grid-label.pdf"));

        CheckCropPositionPdf(
            checks,
            tempDirectory,
            Path.Combine(outputDirectory, "PrintAI-selftest-crop-position.pdf"));
    }

    private static void CheckExifPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var source = Path.Combine(tempDirectory, "oriented.jpg");
        WriteOrientedJpeg(source);

        var thumb = SourceJobRenderer.RenderSourceThumbnailPng(
            source,
            sourcePageIndex: 0,
            maxDimension: 256);

        using var decoded = SKBitmap.Decode(thumb);
        if (decoded is null || decoded.Height <= decoded.Width)
        {
            checks.Add(new(
                "pdf-regression-exif",
                false,
                "EXIF-oriented portrait source did not decode as portrait."));
            return;
        }

        var job = new PrintJobSpec(
            "exif-pdf",
            [new SourceSpec(source)],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                60,
                90,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            new PrintSettings(),
            new PolicySpec());

        RenderJobToPdf(
            checks,
            "pdf-regression-exif",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 1);
    }

    private static void CheckMixedPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var image = Path.Combine(tempDirectory, "mixed-image.png");
        var pdfPage = Path.Combine(tempDirectory, "mixed-pdf-page.png");
        var pdf = Path.Combine(tempDirectory, "mixed-source.pdf");
        WriteSmokePng(image);
        WriteSmokePng(pdfPage);
        ScanPdfWriter.Write([pdfPage, pdfPage], pdf);

        var job = MixedCompositionWorkflow.CreateJob(
            [
                new SourceSpec(image, Copies: 2),
                new SourceSpec(pdf, Copies: 1, PageIndex: 1)
            ],
            new MixedCompositionOptions(
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: true,
                CutMarks: true,
                Fit: FitMode.Contain));

        RenderJobToPdf(
            checks,
            "pdf-regression-mixed",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 1);
    }

    private static void CheckNUpPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var page = Path.Combine(tempDirectory, "nup-page.png");
        var sourcePdf = Path.Combine(tempDirectory, "nup-source.pdf");
        WriteSmokePng(page);
        ScanPdfWriter.Write(Enumerable.Repeat(page, 5), sourcePdf);

        var plan = new PrintPlan(
            "n-up-regression",
            [new PlanSourceSpec(sourcePdf, 5)],
            [
                new PrintOutputGroupSpec(
                    "4-up",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 5)])],
                    new PaperSpec(),
                    new LayoutSpec(
                        LayoutMode.ExactSize,
                        200,
                        287,
                        MarginMm: 5,
                        AllowRotate: false),
                    new OutputPrintSettings(),
                    NUp: new NUpSpec(4))
            ],
            new PolicySpec());

        var job = AssertSingleJob(plan);

        RenderJobToPdf(
            checks,
            "pdf-regression-nup",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 2);
    }

    private static void CheckBookletPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var page = Path.Combine(tempDirectory, "booklet-page.png");
        var sourcePdf = Path.Combine(tempDirectory, "booklet-source.pdf");
        WriteSmokePng(page);
        ScanPdfWriter.Write(Enumerable.Repeat(page, 8), sourcePdf);

        var plan = new PrintPlan(
            "booklet-regression",
            [new PlanSourceSpec(sourcePdf, 8)],
            [
                new PrintOutputGroupSpec(
                    "Booklet",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 8)])],
                    new PaperSpec(),
                    new LayoutSpec(LayoutMode.ExactSize, 200, 287),
                    new OutputPrintSettings(Duplex: DuplexMode.Off),
                    Booklet: new BookletSpec())
            ],
            new PolicySpec());

        var job = AssertSingleJob(plan);

        RenderJobToPdf(
            checks,
            "pdf-regression-booklet",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 4);
    }

    private static void CheckPosterPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var source = Path.Combine(tempDirectory, "poster-source.png");
        WriteSmokePng(source);

        var plan = new PrintPlan(
            "poster-regression",
            [
                new PlanSourceSpec(
                    source,
                    1,
                    PixelWidth: 4000,
                    PixelHeight: 3000)
            ],
            [
                new PrintOutputGroupSpec(
                    "Poster",
                    [new PageSelectionSpec(0, [new PageRangeSpec(1, 1)])],
                    new PaperSpec(),
                    new LayoutSpec(LayoutMode.ExactSize, 200, 287),
                    new OutputPrintSettings(),
                    Poster: new PosterSpec(
                        TargetWidthMm: 420,
                        TargetHeightMm: 594))
            ],
            new PolicySpec());

        var job = AssertSingleJob(plan);
        var expected = SourceJobRenderer.GetOutputPageCount(job);

        if (expected <= 1)
        {
            checks.Add(new(
                "pdf-regression-poster",
                false,
                "Poster regression did not produce multiple tiles."));
            return;
        }

        RenderJobToPdf(
            checks,
            "pdf-regression-poster",
            job,
            outputPath,
            tempDirectory,
            expected);
    }

    private static void CheckCollagePdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var first = Path.Combine(tempDirectory, "collage-a.png");
        var second = Path.Combine(tempDirectory, "collage-b.png");
        var third = Path.Combine(tempDirectory, "collage-c.png");

        WriteColoredPng(first, SKColors.IndianRed, 420, 280);
        WriteColoredPng(second, SKColors.SteelBlue, 280, 420);
        WriteColoredPng(third, SKColors.OliveDrab, 360, 360);

        var template = CollageTemplateLibrary
            .ThreePhoto4x6Portrait()
            .Single(item =>
                item.Id == "center-circle-two-sides");

        var job = CollageTemplateLibrary.CreateJob(
            template,
            [
                new SourceSpec(first),
                new SourceSpec(second),
                new SourceSpec(third)
            ],
            [
                new CollageFrameAssignment(
                    0,
                    2,
                    Scale: 1.15,
                    OffsetX: 0.05,
                    OffsetY: -0.05),
                new CollageFrameAssignment(1, 0),
                new CollageFrameAssignment(2, 1)
            ]);

        if (job.Layout.Mode != LayoutMode.Canvas ||
            job.Layout.Canvas?.Placements.Count != 3)
        {
            checks.Add(new(
                "pdf-regression-collage",
                false,
                "Collage template did not compile to a three-frame canvas."));
            return;
        }

        RenderJobToPdf(
            checks,
            "pdf-regression-collage",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 1);
    }

    private static void CheckAutoLayoutPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var first = Path.Combine(tempDirectory, "auto-a.png");
        var second = Path.Combine(tempDirectory, "auto-b.png");
        var third = Path.Combine(tempDirectory, "auto-c.png");

        WriteColoredPng(first, SKColors.DarkOrange, 500, 300);
        WriteColoredPng(second, SKColors.MediumPurple, 300, 500);
        WriteColoredPng(third, SKColors.Teal, 400, 400);

        var candidate = AutoLayoutWorkflow.Generate4x6(
            [
                new SourceSpec(first),
                new SourceSpec(second),
                new SourceSpec(third)
            ],
            AutoLayoutPreference.Fill)
            .First();

        var layout = PrintAI.Layout.LayoutEngine.Layout(
            candidate.Job);

        if (layout.Placements.Count != 3 ||
            candidate.Job.Layout.Fit != FitMode.Cover)
        {
            checks.Add(new(
                "pdf-regression-auto-layout",
                false,
                "Auto-layout candidate did not preserve three cover-fit placements."));
            return;
        }

        RenderJobToPdf(
            checks,
            "pdf-regression-auto-layout",
            candidate.Job,
            outputPath,
            tempDirectory,
            expectedPages: 1);
    }

    private static void CheckGridLabelPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var source = Path.Combine(tempDirectory, "grid-label.png");
        WriteColoredPng(source, SKColors.Goldenrod, 400, 600);

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

        var layout = PrintAI.Layout.LayoutEngine.Layout(job);

        if (!job.Layout.CutMarks ||
            layout.CapacityPerPage != 18 ||
            !layout.Rotated)
        {
            checks.Add(new(
                "pdf-regression-grid-label",
                false,
                $"Unexpected grid result: capacity={layout.CapacityPerPage}, rotated={layout.Rotated}."));
            return;
        }

        RenderJobToPdf(
            checks,
            "pdf-regression-grid-label",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 2);
    }

    private static void CheckCropPositionPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string tempDirectory,
        string outputPath)
    {
        var source = Path.Combine(tempDirectory, "crop-position.png");
        WriteCropSource(source);

        var job = new PrintJobSpec(
            "crop-position-regression",
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

        var layout = PrintAI.Layout.LayoutEngine.Layout(job);
        var placement = layout.Placements.Single();

        if (placement.XMm < 20)
        {
            checks.Add(new(
                "pdf-regression-crop-position",
                false,
                $"Right-anchored placement violated left margin: x={placement.XMm}."));
            return;
        }

        RenderJobToPdf(
            checks,
            "pdf-regression-crop-position",
            job,
            outputPath,
            tempDirectory,
            expectedPages: 1);
    }

    private static PrintJobSpec AssertSingleJob(PrintPlan plan)
    {
        var compiled = PrintPlanCompiler.Compile(plan);
        if (compiled.Batches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one print batch but found {compiled.Batches.Count}.");
        }

        return compiled.Batches[0].Job;
    }

    private static void RenderJobToPdf(
        ICollection<DesktopSelfTestCheck> checks,
        string checkName,
        PrintJobSpec job,
        string outputPath,
        string tempDirectory,
        int expectedPages)
    {
        var actualPages = SourceJobRenderer.GetOutputPageCount(job);
        if (actualPages != expectedPages)
        {
            checks.Add(new(
                checkName,
                false,
                $"Expected {expectedPages} output page(s), got {actualPages}."));
            return;
        }

        var rendered = new List<string>();
        for (var index = 0; index < actualPages; index++)
        {
            var bytes = SourceJobRenderer.RenderMixedA4(
                job,
                outputPageIndex: index,
                dpi: 72);

            var path = Path.Combine(
                tempDirectory,
                $"{checkName}-{index + 1}.png");
            File.WriteAllBytes(path, bytes);
            rendered.Add(path);
        }

        var paperWidth = job.Paper.Orientation == PageOrientation.Portrait
            ? job.Paper.WidthMm
            : job.Paper.HeightMm;
        var paperHeight = job.Paper.Orientation == PageOrientation.Portrait
            ? job.Paper.HeightMm
            : job.Paper.WidthMm;

        ScanPdfWriter.Write(
            rendered,
            outputPath,
            paperWidth,
            paperHeight);

        var metadata = SourceInspector.Inspect(outputPath);
        var size = new FileInfo(outputPath).Length;
        var passed =
            metadata.Kind == SourceKind.Pdf &&
            metadata.PageCount == expectedPages &&
            size > 1000;

        checks.Add(new(
            checkName,
            passed,
            passed
                ? $"{expectedPages} page(s) -> {Path.GetFileName(outputPath)} ({size} bytes)."
                : $"PDF validation failed: pages={metadata.PageCount}, bytes={size}."));
    }

    private static void WriteColoredPng(
        string path,
        SKColor color,
        int width,
        int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(4, Math.Min(width, height) / 30f)
        };

        canvas.DrawRect(
            width * 0.15f,
            height * 0.15f,
            width * 0.7f,
            height * 0.7f,
            paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        if (data is null)
            throw new InvalidOperationException("Could not encode layout test PNG.");

        File.WriteAllBytes(path, data.ToArray());
    }

    private static void WriteCropSource(string path)
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(SKColors.White);

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.Crimson,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRect(
            new SKRect(50, 40, 160, 170),
            paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        if (data is null)
            throw new InvalidOperationException("Could not encode crop test PNG.");

        File.WriteAllBytes(path, data.ToArray());
    }

    private static void WriteOrientedJpeg(string path)
    {
        using var bitmap = new SKBitmap(40, 20);
        bitmap.Erase(SKColors.CadetBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);

        if (data is null)
            throw new InvalidOperationException("Could not encode EXIF test JPEG.");

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
    }

    private static byte[] CreateExifOrientationSegment(ushort orientation)
    {
        byte[] payload =
        [
            0x45,0x78,0x69,0x66,0x00,0x00,
            0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00,
            0x01,0x00,
            0x12,0x01,0x03,0x00,0x01,0x00,0x00,0x00,
            (byte)orientation,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00
        ];

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
