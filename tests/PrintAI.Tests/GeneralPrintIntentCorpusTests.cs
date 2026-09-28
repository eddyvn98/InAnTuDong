using System.Text.Json;
using Xunit;

namespace PrintAI.Tests;

public sealed class GeneralPrintIntentCorpusTests
{
    [Fact]
    public void Corpus_HasBroadStableCoverage()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "general-print-intent-corpus.json");

        var corpus = JsonSerializer.Deserialize<CorpusFile>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("Corpus could not be parsed.");

        Assert.Equal("1.0", corpus.SchemaVersion);
        Assert.Equal(120, corpus.Cases.Count);
        Assert.Equal(120, corpus.Cases.Select(item => item.Id).Distinct().Count());
        Assert.Equal(12, corpus.Cases.Select(item => item.Category).Distinct().Count());

        foreach (var group in corpus.Cases.GroupBy(item => item.Category))
            Assert.Equal(10, group.Count());

        Assert.All(corpus.Cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Request));
            Assert.NotEmpty(item.SourcePageCounts);
            Assert.All(item.SourcePageCounts, count => Assert.True(count >= 1));
            Assert.NotEmpty(item.Features);
            Assert.Contains(item.ExpectedSupport, new[] { "supported", "planned" });
        });

        Assert.True(corpus.Cases.Count(item => item.ExpectedSupport == "supported") >= 60);
        Assert.True(corpus.Cases.Count(item => item.ExpectedSupport == "planned") >= 30);
    }

    private sealed record CorpusFile(
        string SchemaVersion,
        string Description,
        IReadOnlyList<CorpusCase> Cases);

    private sealed record CorpusCase(
        string Id,
        string Category,
        string Request,
        IReadOnlyList<int> SourcePageCounts,
        IReadOnlyList<string> Features,
        string ExpectedSupport);
}
