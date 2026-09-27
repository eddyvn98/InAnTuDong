using System.Text.Json;

namespace PrintAI.History;

public sealed record JobHistoryEntry(
    DateTimeOffset CreatedAt,
    string Action,
    string Status,
    string? Request = null,
    string? Printer = null,
    string? JobName = null,
    string? Detail = null);

public sealed class JobHistoryStore
{
    private readonly string _path;
    private readonly int _maxEntries;
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public JobHistoryStore(string path, int maxEntries = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (maxEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        _path = path;
        _maxEntries = maxEntries;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    public void Append(JobHistoryEntry entry)
    {
        var items = Read()
            .Prepend(entry)
            .Take(_maxEntries)
            .ToArray();

        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(items, _jsonOptions));
        File.Move(temp, _path, overwrite: true);
    }

    public IReadOnlyList<JobHistoryEntry> Read()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<JobHistoryEntry[]>(
                File.ReadAllText(_path),
                _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
