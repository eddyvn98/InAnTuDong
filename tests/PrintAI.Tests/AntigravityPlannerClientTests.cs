using PrintAI.Planning;

namespace PrintAI.Tests;

public sealed class AntigravityPlannerClientTests
{
    [Fact]
    public async Task CompleteAsync_UsesFastTier_WhenFastPayloadIsConfident()
    {
        var runner = new FakeRunner(
            Success("""{"confidence":0.95,"route":"fast"}"""));

        var client = CreateClient(runner);

        var result = await client.CompleteAsync(
            new PlannerModelRequest(
                "Return a routing object.",
                """{"request":"in 2 ban"}"""));

        Assert.Equal("""{"confidence":0.95,"route":"fast"}""", result);
        Assert.Single(runner.Invocations);
        Assert.Equal("fast-model", runner.Invocations[0].Model);
        Assert.Equal("fast", client.LastExecution?.Tier);
        Assert.False(client.LastExecution?.Escalated);
    }

    [Fact]
    public async Task CompleteAsync_Escalates_WhenFastConfidenceIsLow()
    {
        var runner = new FakeRunner(
            Success("""{"confidence":0.40,"route":"fast"}"""),
            Success("""{"confidence":0.96,"route":"deep"}"""));

        var client = CreateClient(runner);

        var result = await client.CompleteAsync(
            new PlannerModelRequest(
                "Return a routing object.",
                """{"request":"complex request"}"""));

        Assert.Equal("""{"confidence":0.96,"route":"deep"}""", result);
        Assert.Equal(2, runner.Invocations.Count);
        Assert.Equal("fast-model", runner.Invocations[0].Model);
        Assert.Equal("deep-model", runner.Invocations[1].Model);
        Assert.Equal("deep", client.LastExecution?.Tier);
        Assert.True(client.LastExecution?.Escalated);
        Assert.Equal("low-confidence", client.LastExecution?.Reason);
    }

    [Fact]
    public async Task CompleteAsync_Escalates_WhenFastTransportFails()
    {
        var runner = new FakeRunner(
            new AntigravityCommandResult(1, "", "fast failed"),
            Success("""{"route":"deep"}"""));

        var client = CreateClient(runner);

        var result = await client.CompleteAsync(
            new PlannerModelRequest(
                "Return a routing object.",
                """{"request":"retry"}"""));

        Assert.Equal("""{"route":"deep"}""", result);
        Assert.Equal(2, runner.Invocations.Count);
        Assert.Equal("fast-transport-failure", client.LastExecution?.Reason);
    }

    private static AntigravityPlannerClient CreateClient(
        IAntigravityCommandRunner runner) =>
        new(
            new AntigravityPlannerOptions(
                CliPath: "agy.exe",
                FastModel: "fast-model",
                DeepModel: "deep-model",
                FastEffort: "low",
                DeepEffort: "high",
                EscalateBelowConfidence: 0.80),
            runner);

    private static AntigravityCommandResult Success(string payload) =>
        new(
            0,
            $$"""{"status":"SUCCESS","structured_output":{{payload}}}""",
            "");

    private sealed class FakeRunner(
        params AntigravityCommandResult[] results) :
        IAntigravityCommandRunner
    {
        private readonly Queue<AntigravityCommandResult> _results =
            new(results);

        public List<AntigravityInvocation> Invocations { get; } = [];

        public Task<AntigravityCommandResult> RunAsync(
            AntigravityInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(invocation);

            if (_results.Count == 0)
                throw new InvalidOperationException("No fake result remains.");

            return Task.FromResult(_results.Dequeue());
        }
    }
}
