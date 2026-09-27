using System.Drawing;
using System.Drawing.Printing;
using System.Printing;
using System.Runtime.InteropServices;

namespace PrintAI.Windows.Printing;

public static class WindowsSpoolerPrinter
{
    public static PrintSubmissionResult SubmitA4Png(
        string printerName,
        string pngPath,
        PrinterDeviceProfile? profile = null,
        short copies = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);

        if (!File.Exists(pngPath))
            return Failed(printerName, "unknown", $"PNG file not found: {pngPath}");

        if (copies <= 0)
            return Failed(printerName, "unknown", "Copies must be greater than zero.");

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

            var a4 = document.PrinterSettings.PaperSizes
                .Cast<PaperSize>()
                .FirstOrDefault(IsA4);

            if (a4 is null)
                return Failed(printerName, documentName, "The printer driver does not advertise A4 paper.");

            document.DefaultPageSettings.PaperSize = a4;
            document.DefaultPageSettings.Landscape = false;
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

                var targetWidth = (float)(210d * profile.ScaleX);
                var targetHeight = (float)(297d * profile.ScaleY);

                e.Graphics.DrawImage(
                    image,
                    new RectangleF(0, 0, targetWidth, targetHeight),
                    0,
                    0,
                    image.Width,
                    image.Height,
                    GraphicsUnit.Pixel);

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
        catch (Exception ex) when (
            ex is InvalidPrinterException or
            PrintSystemException or
            ExternalException or
            ArgumentException or
            InvalidOperationException or
            SystemException)
        {
            return Failed(printerName, documentName, ex.Message);
        }
    }

    private static bool IsA4(PaperSize paper)
    {
        if (paper.Kind == PaperKind.A4)
            return true;

        var widthMm = PrinterCapabilityProbe.HundredthsInchToMm(paper.Width);
        var heightMm = PrinterCapabilityProbe.HundredthsInchToMm(paper.Height);
        var width = Math.Min(widthMm, heightMm);
        var height = Math.Max(widthMm, heightMm);

        return Math.Abs(width - 210) <= 2 && Math.Abs(height - 297) <= 2;
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
