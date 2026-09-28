using System.Text.Json;
using Xunit;

namespace PrintAI.Tests;

public sealed class PrintIntentCorpusTests
{
    private const string FixtureName = "print-intent-corpus.json";

    [Fact]
    public void Corpus_Has150UniqueBalancedCases()
    {
        using var document = LoadCorpus();
        var cases = document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(150, cases.Length);

        var ids = cases
            .Select(item => item.GetProperty("id").GetString())
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(ids, string.IsNullOrWhiteSpace);

        var categories = cases
            .GroupBy(item => item.GetProperty("category").GetString())
            .ToDictionary(group => group.Key!, group => group.Count());

        Assert.Equal(15, categories.Count);
        Assert.All(categories, entry => Assert.Equal(10, entry.Value));
    }

    [Fact]
    public void Corpus_UsesOnlyKnownCoverageLabelsAndExplicitGapCapabilities()
    {
        using var document = LoadCorpus();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "supported",
            "supportedWithCapability",
            "clarification",
            "gap"
        };

        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var request = item.GetProperty("request").GetString();
            var coverage = item.GetProperty("expectedCoverage").GetString();

            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.False(string.IsNullOrWhiteSpace(request));
            Assert.Contains(coverage!, allowed);

            var hasGap = item.TryGetProperty("gapCapability", out var gap) &&
                         gap.ValueKind == JsonValueKind.String &&
                         !string.IsNullOrWhiteSpace(gap.GetString());

            if (coverage == "gap")
                Assert.True(hasGap, $"{id} must name the missing capability.");
        }
    }

    [Fact]
    public void Corpus_CurrentCoverageBaselineIsIntentional()
    {
        using var document = LoadCorpus();
        var counts = document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .GroupBy(item => item.GetProperty("expectedCoverage").GetString())
            .ToDictionary(group => group.Key!, group => group.Count());

        Assert.Equal(120, counts["supported"]);
        Assert.Equal(10, counts["supportedWithCapability"]);
        Assert.Equal(10, counts["clarification"]);
        Assert.Equal(10, counts["gap"]);
    }

    [Fact]
    public void Corpus_KeepsKnownGapFamiliesVisible()
    {
        using var document = LoadCorpus();
        var gaps = document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Where(item => item.GetProperty("expectedCoverage").GetString() == "gap")
            .Select(item => item.GetProperty("gapCapability").GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("generalNUp", gaps);
        Assert.DoesNotContain("customScalePercent", gaps);
        Assert.DoesNotContain("shrinkOnly", gaps);
        Assert.DoesNotContain("maxFitWithMargin", gaps);
        Assert.DoesNotContain("asymmetricMargins", gaps);
        Assert.DoesNotContain("contentOffset", gaps);
        Assert.DoesNotContain("anchorPosition", gaps);
        Assert.DoesNotContain("cropRegion", gaps);
        Assert.DoesNotContain("autoCropForPrint", gaps);
        Assert.DoesNotContain("bookletImposition", gaps);
        Assert.DoesNotContain("posterTiling", gaps);
        Assert.Contains("variableItemSizes", gaps);
    }

    private static JsonDocument LoadCorpus()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            FixtureName);

        Assert.True(File.Exists(path), $"Corpus fixture is missing: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
