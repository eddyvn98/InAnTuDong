using System.IO;
using System.Text.Json;
using PrintAI.Domain;
using PrintAI.Windows.Printing;

namespace PrintAI.Desktop;

internal sealed record StoredManualDuplexProfile(
    string PrinterName,
    ManualDuplexBackOrder BackOrder,
    int LongEdgeBackRotationDegrees,
    int ShortEdgeBackRotationDegrees,
    string ReinsertInstruction,
    bool IsVerified,
    DateTimeOffset UpdatedAt)
{
    public ManualDuplexProfile ToProfile() =>
        new(
            IsVerified,
            BackOrder,
            LongEdgeBackRotationDegrees,
            ShortEdgeBackRotationDegrees,
            ReinsertInstruction);
}

internal sealed class ManualDuplexCalibrationStore(string path)
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public StoredManualDuplexProfile? Get(string printerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        return Load()
            .FirstOrDefault(profile =>
                string.Equals(
                    profile.PrinterName,
                    printerName,
                    StringComparison.OrdinalIgnoreCase));
    }

    public void Save(StoredManualDuplexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var profiles = Load()
            .Where(existing =>
                !string.Equals(
                    existing.PrinterName,
                    profile.PrinterName,
                    StringComparison.OrdinalIgnoreCase))
            .Append(profile)
            .OrderBy(item => item.PrinterName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(profiles, Options));

        File.Move(temporary, path, overwrite: true);
    }

    private IReadOnlyList<StoredManualDuplexProfile> Load()
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<StoredManualDuplexProfile[]>(
                       File.ReadAllText(path),
                       Options)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }
}
