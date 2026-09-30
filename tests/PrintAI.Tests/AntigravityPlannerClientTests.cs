using System.Text.Json;
using Xunit;
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

    [Fact]
    public async Task CompleteAsync_UsesStrictPrintJobSchema_AndOmitsModelEncodedEffort()
    {
        const string payload = """{"job":{"jobName":"test","sources":[{"path":"fixture.png"}],"paper":{},"layout":{"mode":"exactSize","itemWidthMm":40,"itemHeightMm":60},"print":{},"policy":{}},"confidence":0.95,"questions":[],"warnings":[]}""";
        var runner = new FakeRunner(Success(payload));
        var options = new AntigravityPlannerOptions(
            CliPath: "agy",
            FastModel: "gemini-3.6-flash-medium",
            DeepModel: "gemini-3.6-flash-high",
            FastEffort: "auto",
            DeepEffort: "auto");
        var client = new AntigravityPlannerClient(options, runner);

        await client.CompleteAsync(new PlannerModelRequest(
            "job.schemaVersion must be \"1.0\"",
            "{}"));

        var invocation = Assert.Single(runner.Invocations);
        Assert.Null(invocation.Effort);
        using var schema = System.Text.Json.JsonDocument.Parse(invocation.JsonSchema);
        var root = schema.RootElement;
        var jobSchema = root.GetProperty("properties").GetProperty("job");
        if (jobSchema.TryGetProperty("$ref", out var reference))
        {
            var definitionName = reference.GetString()!.Split('/').Last();
            jobSchema = root.GetProperty("$defs").GetProperty(definitionName);
        }

        Assert.Equal(JsonValueKind.False, root.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.False, jobSchema.GetProperty("additionalProperties").ValueKind);
        Assert.False(jobSchema.GetProperty("properties").TryGetProperty("items", out _));
        Assert.True(jobSchema.GetProperty("properties").TryGetProperty("jobName", out _));
        Assert.Equal("fast", client.LastExecution?.Tier);
    }

    [Fact]
    public async Task CompleteAsync_UsesStrictPrintPlanSchema()
    {
        var runner = new FakeRunner(Success(
            """{"plan":{"planName":"test","sources":[{"path":"fixture.png","pageCount":1}],"outputGroups":[{"name":"A4","selections":[{"sourceIndex":0,"include":[{"startPage":1,"endPage":1}],"exclude":[],"parity":"all"}],"paper":{"widthMm":210,"heightMm":297,"orientation":"portrait"},"layout":{"mode":"exactSize","itemWidthMm":40,"itemHeightMm":60},"print":{},"sets":1,"collate":true,"sequence":0}],"policy":{},"schemaVersion":"2.0"},"confidence":0.95,"questions":[],"warnings":[]}"""));
        var client = CreateClient(runner);

        await client.CompleteAsync(new PlannerModelRequest(
            "plan.schemaVersion must be \"2.0\"",
            "{}"));

        using var schema = JsonDocument.Parse(Assert.Single(runner.Invocations).JsonSchema);
        var root = schema.RootElement;
        var planSchema = ResolveSchema(root, root.GetProperty("properties").GetProperty("plan"));
        var groupSchema = ResolveSchema(
            root,
            ResolveSchema(root, planSchema.GetProperty("properties").GetProperty("outputGroups"))
                .GetProperty("items"));

        Assert.Equal(JsonValueKind.False, root.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.False, planSchema.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.False, groupSchema.GetProperty("additionalProperties").ValueKind);
        Assert.False(planSchema.GetProperty("properties").TryGetProperty("summary", out _));
        Assert.True(planSchema.GetProperty("properties").TryGetProperty("planName", out _));
    }

    [Fact]
    public void Options_RejectEffortThatConflictsWithModelTier()
    {
        var options = new AntigravityPlannerOptions(
            CliPath: "agy",
            FastModel: "gemini-3.6-flash-medium",
            DeepModel: "gemini-3.6-flash-high",
            FastEffort: "low",
            DeepEffort: "auto");

        Assert.Throws<ArgumentException>(options.Validate);
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

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
            return schema;

        var definitionName = reference.GetString()!.Split('/').Last();
        return root.GetProperty("$defs").GetProperty(definitionName);
    }

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
