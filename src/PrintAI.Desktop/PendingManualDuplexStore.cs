using System.IO;
using System.Text.Json;
using PrintAI.Domain;

namespace PrintAI.Desktop;

internal sealed record PendingManualDuplexSide(
    string PngPath,
    int RotationDegrees);

internal sealed record PendingManualDuplexJob(
    string Id,
    string JobName,
    string PrinterName,
    string PrinterProfileId,
    DuplexMode Mode,
    DateTimeOffset CreatedAt,
    int SheetCount,
    bool ProfileVerified,
    string ReinsertInstruction,
    double PaperWidthMm,
    double PaperHeightMm,
    bool Landscape,
    string JobFingerprint,
    string ArtifactDirectory,
    IReadOnlyList<PendingManualDuplexSide> BackPass);

internal sealed class PendingManualDuplexStore(string path)
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public PendingManualDuplexJob? Load()
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingManualDuplexJob>(
                File.ReadAllText(path),
                Options);
        }
        catch
        {
            return null;
        }
    }

    public void Save(PendingManualDuplexJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(job, Options));

        File.Move(temporary, path, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
