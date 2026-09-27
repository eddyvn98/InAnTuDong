namespace PrintAI.Windows.Printing;

public static class PrinterMatcher
{
    public static PrinterCapabilitySnapshot? FindBest(
        IEnumerable<PrinterCapabilitySnapshot> printers,
        string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalized = query.Trim();

        return printers
            .Select(printer => new
            {
                Printer = printer,
                Score = Score(printer.Name, normalized)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Printer.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Printer)
            .FirstOrDefault();
    }

    private static int Score(string printerName, string query)
    {
        if (printerName.Equals(query, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (printerName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 80;

        var tokens = query.Split(
            [' ', '-', '_'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.Length > 0 &&
               tokens.All(token => printerName.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? 60
            : 0;
    }
}
