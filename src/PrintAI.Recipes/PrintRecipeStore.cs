using System.Text.Json;
using PrintAI.Domain;

namespace PrintAI.Recipes;

public sealed record PrintRecipe(
    string Id,
    string Name,
    LayoutSpec Layout,
    PrintSettings Print,
    PolicySpec Policy,
    int SourceCopies = 1,
    bool DirectPrintEligible = false,
    DateTimeOffset? UpdatedAt = null)
{
    public PrintJobSpec CreateJob(
        string sourcePath,
        string? jobName = null) =>
        new(
            jobName ?? Name,
            [new SourceSpec(sourcePath, SourceCopies)],
            new PaperSpec(),
            Layout,
            Print,
            Policy);
}

public sealed class PrintRecipeStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public PrintRecipeStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    public IReadOnlyList<PrintRecipe> Read()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<PrintRecipe[]>(
                File.ReadAllText(_path),
                _json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public PrintRecipe Save(PrintRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var validation = PrintJobValidator.Validate(
            recipe.CreateJob("recipe-source.png"));

        if (!validation.IsValid)
        {
            throw new ArgumentException(
                string.Join(
                    "; ",
                    validation.Errors.Select(e => $"{e.Code}: {e.Message}")));
        }

        var normalized = recipe with
        {
            Id = string.IsNullOrWhiteSpace(recipe.Id)
                ? Guid.NewGuid().ToString("N")
                : recipe.Id.Trim(),
            Name = recipe.Name.Trim(),
            UpdatedAt = DateTimeOffset.Now
        };

        if (string.IsNullOrWhiteSpace(normalized.Name))
            throw new ArgumentException("Recipe name is required.");

        var items = Read()
            .Where(x => !string.Equals(
                x.Id,
                normalized.Id,
                StringComparison.OrdinalIgnoreCase))
            .Append(normalized)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Persist(items);
        return normalized;
    }

    public bool Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var current = Read();
        var remaining = current
            .Where(x => !string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (remaining.Length == current.Count)
            return false;

        Persist(remaining);
        return true;
    }

    private void Persist(IReadOnlyList<PrintRecipe> recipes)
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(recipes, _json));
        File.Move(temp, _path, overwrite: true);
    }
}
