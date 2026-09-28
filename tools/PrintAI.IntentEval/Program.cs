using System.Text.Json;
using PrintAI.Domain;
using PrintAI.Planning;

var endpointText = Environment.GetEnvironmentVariable("PRINTAI_AI_ENDPOINT");
var model = Environment.GetEnvironmentVariable("PRINTAI_AI_MODEL");
var apiKey = Environment.GetEnvironmentVariable("PRINTAI_AI_API_KEY");

if (string.IsNullOrWhiteSpace(endpointText) ||
    string.IsNullOrWhiteSpace(model) ||
    !Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
{
    Console.Error.WriteLine(
        "Set PRINTAI_AI_ENDPOINT and PRINTAI_AI_MODEL before running the evaluator.");
    return 2;
}

var corpusPath = "tests/fixtures/general-print-intent-corpus.json";
var limit = 10;
string? onlyCase = null;
var supportedOnly = false;

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--all":
            limit = int.MaxValue;
            break;
        case "--limit" when index + 1 < args.Length:
            if (!int.TryParse(args[++index], out limit) || limit < 1)
                throw new ArgumentException("--limit must be a positive integer.");
            break;
        case "--case" when index + 1 < args.Length:
            onlyCase = args[++index];
            break;
        case "--supported-only":
            supportedOnly = true;
            break;
        case "--corpus" when index + 1 < args.Length:
            corpusPath = args[++index];
            break;
        default:
            throw new ArgumentException($"Unknown argument: {args[index]}");
    }
}

var corpus = JsonSerializer.Deserialize<CorpusFile>(
    File.ReadAllText(corpusPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("Corpus could not be parsed.");

IEnumerable<CorpusCase> selected = corpus.Cases;

if (!string.IsNullOrWhiteSpace(onlyCase))
    selected = selected.Where(item =>
        string.Equals(item.Id, onlyCase, StringComparison.OrdinalIgnoreCase));

if (supportedOnly)
    selected = selected.Where(item => item.ExpectedSupport == "supported");

var cases = selected.Take(limit).ToArray();
if (cases.Length == 0)
{
    Console.Error.WriteLine("No corpus cases matched the requested filters.");
    return 2;
}

using var httpClient = new HttpClient();
var client = new ChatCompletionPlannerClient(
    httpClient,
    new ChatCompletionTransportOptions(endpoint, model, apiKey));
var planner = new GeneralPrintPlanner(client);

var passed = 0;
var semanticFailed = 0;
var formatFailed = 0;
var planned = 0;

Console.WriteLine(
    $"PrintAI intent evaluator · model={model} · cases={cases.Length}");

foreach (var item in cases)
{
    var sources = item.SourcePageCounts
        .Select((pageCount, sourceIndex) => new PlanningSource(
            Path: $"C:/printai-corpus/{item.Id}-source-{sourceIndex + 1}.pdf",
            Kind: "Pdf",
            PageCount: pageCount))
        .ToArray();

    try
    {
        var outcome = await planner.PlanAsync(
            new PlanningRequest(item.Request, sources));

        var bound = GeneralPrintPlanSourceBinder.BindToAllowedSources(
            outcome.Plan,
            sources);

        var compiled = PrintPlanCompiler.Compile(bound);

        if (item.ExpectedSupport == "planned")
        {
            planned++;
            Console.WriteLine(
                $"TARGET {item.Id} · groups={bound.OutputGroups.Count} · " +
                $"batches={compiled.Batches.Count} · {item.Request}");
            continue;
        }

        var issues = FeatureExpectations.Check(item, bound);
        if (issues.Count == 0)
        {
            passed++;
            Console.WriteLine(
                $"PASS   {item.Id} · groups={bound.OutputGroups.Count} · " +
                $"batches={compiled.Batches.Count}");
        }
        else
        {
            semanticFailed++;
            Console.WriteLine(
                $"FAIL   {item.Id} · {string.Join(" | ", issues)}");
            Console.WriteLine($"       request: {item.Request}");
        }
    }
    catch (Exception ex)
    {
        formatFailed++;
        Console.WriteLine(
            $"ERROR  {item.Id} · {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(
    $"Summary: pass={passed}, semantic-fail={semanticFailed}, " +
    $"error={formatFailed}, planned-target={planned}");

return semanticFailed == 0 && formatFailed == 0 ? 0 : 1;

static class FeatureExpectations
{
    public static IReadOnlyList<string> Check(
        CorpusCase item,
        PrintPlan plan)
    {
        var issues = new List<string>();
        var groups = plan.OutputGroups;
        var selections = groups.SelectMany(group => group.Selections).ToArray();
        var features = item.Features.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Need("multi-source",
            () => plan.Sources.Count > 1,
            "expected multiple sources");
        Need("output-groups",
            () => groups.Count > 1,
            "expected multiple output groups");
        Need("multiple-output-groups",
            () => groups.Count >= 3,
            "expected at least three output groups");
        Need("page-range",
            () => selections.Any(selection => selection.Include is { Count: > 0 }),
            "expected explicit page ranges");
        Need("multiple-ranges",
            () => selections.Any(selection => selection.Include is { Count: > 1 }),
            "expected multiple page ranges");
        Need("exclude",
            () => selections.Any(selection => selection.Exclude is { Count: > 0 }),
            "expected excluded pages");
        Need("parity-odd",
            () => selections.Any(selection => selection.Parity == PageParity.Odd),
            "expected odd-page parity");
        Need("parity-even",
            () => selections.Any(selection => selection.Parity == PageParity.Even),
            "expected even-page parity");
        Need("sets",
            () => groups.Any(group => group.Sets > 1),
            "expected repeated sets/copies");
        Need("collate",
            () => groups.Any(group => group.Sets > 1 && group.Collate),
            "expected collated sets");
        Need("uncollated",
            () => groups.Any(group => group.Sets > 1 && !group.Collate),
            "expected uncollated copies");
        Need("grayscale",
            () => groups.Any(group => group.Print.ColorMode == ColorMode.Grayscale),
            "expected grayscale output");
        Need("color",
            () => groups.Any(group => group.Print.ColorMode == ColorMode.Color),
            "expected color output");
        Need("mixed-color",
            () => groups.Select(group => group.Print.ColorMode).Distinct().Count() > 1,
            "expected mixed color modes");
        Need("duplex-long-edge",
            () => groups.Any(group => group.Print.Duplex == DuplexMode.LongEdge),
            "expected long-edge duplex");
        Need("duplex-short-edge",
            () => groups.Any(group => group.Print.Duplex == DuplexMode.ShortEdge),
            "expected short-edge duplex");
        Need("simplex",
            () => groups.All(group => group.Print.Duplex == DuplexMode.Off),
            "expected simplex output");
        Need("mixed-duplex",
            () => groups.Select(group => group.Print.Duplex).Distinct().Count() > 1,
            "expected mixed duplex modes");
        Need("quality-high",
            () => groups.Any(group => group.Print.Quality == PrintQuality.High),
            "expected high quality");
        Need("quality-draft",
            () => groups.Any(group => group.Print.Quality == PrintQuality.Draft),
            "expected draft quality");
        Need("landscape",
            () => groups.Any(group => group.Paper.Orientation == PageOrientation.Landscape),
            "expected landscape paper");
        Need("portrait",
            () => groups.Any(group => group.Paper.Orientation == PageOrientation.Portrait),
            "expected portrait paper");
        Need("a4",
            () => groups.All(group =>
                Near(group.Paper.WidthMm, 210) &&
                Near(group.Paper.HeightMm, 297)),
            "expected A4 paper");
        Need("paper-4x6-inch",
            () => groups.Any(group =>
                Near(Math.Min(group.Paper.WidthMm, group.Paper.HeightMm), 101.6) &&
                Near(Math.Max(group.Paper.WidthMm, group.Paper.HeightMm), 152.4)),
            "expected 4x6-inch paper");
        Need("preview-required",
            () => plan.Policy.Preview == PreviewPolicy.Required,
            "expected required preview");

        return issues;

        void Need(
            string feature,
            Func<bool> predicate,
            string message)
        {
            if (features.Contains(feature) && !predicate())
                issues.Add(message);
        }
    }

    private static bool Near(double actual, double expected) =>
        Math.Abs(actual - expected) <= 0.2;
}

sealed record CorpusFile(
    string SchemaVersion,
    string Description,
    IReadOnlyList<CorpusCase> Cases);

sealed record CorpusCase(
    string Id,
    string Category,
    string Request,
    IReadOnlyList<int> SourcePageCounts,
    IReadOnlyList<string> Features,
    string ExpectedSupport);
