using System.Drawing;
using System.IO;
using System.Drawing.Printing;
using System.Printing;

namespace PrintAI.Windows.Printing;

public static class WindowsSpoolerPrinter
{
    public static PrintSubmissionResult SubmitA4Png(
        string printerName,
        string pngPath,
        PrinterDeviceProfile? profile = null,
        short copies = 1) =>
        SubmitPng(
            printerName,
            pngPath,
            paperWidthMm: 210,
            paperHeightMm: 297,
            landscape: false,
            profile,
            copies);

    public static PrintSubmissionResult SubmitPng(
        string printerName,
        string pngPath,
        double paperWidthMm,
        double paperHeightMm,
        bool landscape,
        PrinterDeviceProfile? profile = null,
        short copies = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);

        if (!File.Exists(pngPath))
            return Failed(printerName, "unknown", $"PNG file not found: {pngPath}");

        if (copies <= 0)
            return Failed(printerName, "unknown", "Copies must be greater than zero.");

        if (paperWidthMm <= 0 || paperHeightMm <= 0)
            return Failed(printerName, "unknown", "Paper dimensions must be positive.");

        profile ??= new PrinterDeviceProfile("default", printerName);

        var documentName = $"PrintAI-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

        try
        {
            using var image = Image.FromFile(pngPath);
            using var document = new PrintDocument();

            document.DocumentName = documentName;
            document.OriginAtMargins = false;
            document.PrintController = new StandardPrintController();
            document.PrinterSettings.PrinterName = printerName;
            document.PrinterSettings.Copies = copies;

            if (!document.PrinterSettings.IsValid)
                return Failed(printerName, documentName, "Windows reports the printer as invalid.");

            var paper = FindPaper(
                document.PrinterSettings,
                paperWidthMm,
                paperHeightMm);

            if (paper is null)
            {
                return Failed(
                    printerName,
                    documentName,
                    $"The printer driver does not advertise paper near " +
                    $"{paperWidthMm:0.#} x {paperHeightMm:0.#} mm.");
            }

            document.DefaultPageSettings.PaperSize = paper;
            document.DefaultPageSettings.Landscape = landscape;
            document.DefaultPageSettings.Color = document.PrinterSettings.SupportsColor;

            document.PrintPage += (_, e) =>
            {
                if (e.Graphics is null)
                    throw new InvalidOperationException("Printer graphics context is unavailable.");

                e.Graphics.PageUnit = GraphicsUnit.Millimeter;

                var hardMarginX = PrinterCapabilityProbe.HundredthsInchToMm(
                    e.PageSettings.HardMarginX);
                var hardMarginY = PrinterCapabilityProbe.HundredthsInchToMm(
                    e.PageSettings.HardMarginY);

                e.Graphics.TranslateTransform(
                    (float)(-hardMarginX + profile.OffsetXMm),
                    (float)(-hardMarginY + profile.OffsetYMm));

                var logicalWidth = landscape ? paperHeightMm : paperWidthMm;
                var logicalHeight = landscape ? paperWidthMm : paperHeightMm;
                var targetWidth = (float)(logicalWidth * profile.ScaleX);
                var targetHeight = (float)(logicalHeight * profile.ScaleY);

                e.Graphics.DrawImage(
                    image,
                    new RectangleF(0, 0, targetWidth, targetHeight));

                e.HasMorePages = false;
            };

            document.Print();

            var job = SpoolerJobMonitor.FindByDocumentName(printerName, documentName);

            return new(
                State: job is null
                    ? PrintSubmissionState.SubmittedUntracked
                    : PrintSubmissionState.Submitted,
                PrinterName: printerName,
                DocumentName: documentName,
                JobId: job?.JobId);
        }
        catch (SystemException ex)
        {
            return Failed(printerName, documentName, ex.Message);
        }
    }

    public static bool MatchesPaperSize(
        PaperCapability paper,
        double widthMm,
        double heightMm,
        double toleranceMm = 2)
    {
        var requestedShort = Math.Min(widthMm, heightMm);
        var requestedLong = Math.Max(widthMm, heightMm);
        var actualShort = Math.Min(paper.WidthMm, paper.HeightMm);
        var actualLong = Math.Max(paper.WidthMm, paper.HeightMm);

        return Math.Abs(actualShort - requestedShort) <= toleranceMm &&
               Math.Abs(actualLong - requestedLong) <= toleranceMm;
    }

    private static PaperSize? FindPaper(
        PrinterSettings settings,
        double widthMm,
        double heightMm)
    {
        foreach (PaperSize paper in settings.PaperSizes)
        {
            var actualWidth = PrinterCapabilityProbe.HundredthsInchToMm(paper.Width);
            var actualHeight = PrinterCapabilityProbe.HundredthsInchToMm(paper.Height);

            if (Matches(
                    actualWidth,
                    actualHeight,
                    widthMm,
                    heightMm))
            {
                return paper;
            }
        }

        return null;
    }

    private static bool Matches(
        double actualWidth,
        double actualHeight,
        double requestedWidth,
        double requestedHeight)
    {
        var actualShort = Math.Min(actualWidth, actualHeight);
        var actualLong = Math.Max(actualWidth, actualHeight);
        var requestedShort = Math.Min(requestedWidth, requestedHeight);
        var requestedLong = Math.Max(requestedWidth, requestedHeight);

        return Math.Abs(actualShort - requestedShort) <= 2 &&
               Math.Abs(actualLong - requestedLong) <= 2;
    }

    private static PrintSubmissionResult Failed(
        string printerName,
        string documentName,
        string error) =>
        new(
            PrintSubmissionState.Failed,
            printerName,
            documentName,
            Error: error);
}
