using System.Text.Json;
using PrintAI.Windows.Printing;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

if (args.Length >= 1 &&
    args[0].Equals("print", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Write(new { error = "Usage: print <printer-query> <a4-png-path> [copies]" });
        return;
    }

    var query = args[1];
    var path = args[2];
    var copies = args.Length >= 4 && short.TryParse(args[3], out var parsedCopies)
        ? parsedCopies
        : (short)1;

    var printers = PrinterCapabilityProbe.Enumerate();
    var match = PrinterMatcher.FindBest(printers, query);

    if (match is null)
    {
        Write(new
        {
            error = $"No installed printer matches '{query}'.",
            installed = printers.Select(p => p.Name).ToArray()
        });
        return;
    }

    var profile = query.Contains("L3310", StringComparison.OrdinalIgnoreCase) ||
                  match.Name.Contains("L3310", StringComparison.OrdinalIgnoreCase)
        ? PrinterDeviceProfile.EpsonL3310Calibrated
        : new PrinterDeviceProfile("default", match.Name);

    Write(WindowsSpoolerPrinter.SubmitA4Png(
        match.Name,
        path,
        profile,
        copies));
    return;
}

if (args.Length >= 1 &&
    args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3 || !int.TryParse(args[2], out var jobId))
    {
        Write(new { error = "Usage: status <exact-printer-name> <job-id>" });
        return;
    }

    var status = SpoolerJobMonitor.GetStatus(args[1], jobId);
    Write(status is null
        ? new { printer = args[1], jobId, state = "not-found-or-already-completed" }
        : status);
    return;
}

var queryText = args.Length == 0 ? null : string.Join(' ', args);
var installedPrinters = PrinterCapabilityProbe.Enumerate();

object output = queryText is null
    ? new
    {
        count = installedPrinters.Count,
        printers = installedPrinters
    }
    : new
    {
        query = queryText,
        match = PrinterMatcher.FindBest(installedPrinters, queryText),
        installed = installedPrinters.Select(p => p.Name).ToArray()
    };

Write(output);

void Write(object value) =>
    Console.WriteLine(JsonSerializer.Serialize(value, jsonOptions));
