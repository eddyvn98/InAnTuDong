using System.Text.Json;
using PrintAI.Windows.Printing;

var query = args.Length == 0 ? null : string.Join(' ', args);
var printers = PrinterCapabilityProbe.Enumerate();

object output = query is null
    ? new
    {
        count = printers.Count,
        printers
    }
    : new
    {
        query,
        match = PrinterMatcher.FindBest(printers, query),
        installed = printers.Select(p => p.Name).ToArray()
    };

Console.WriteLine(JsonSerializer.Serialize(
    output,
    new JsonSerializerOptions { WriteIndented = true }));
