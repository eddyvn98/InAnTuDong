using System.Net;
using System.Text;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class ChatCompletionPlannerClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsConfiguredModelAndReturnsContent()
    {
        string? requestBody = null;

        var handler = new FakeHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"choices":[{"message":{"content":"{"job":{}}"}}]}
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var planner = new ChatCompletionPlannerClient(
            client,
            new ChatCompletionTransportOptions(
                new Uri("https://example.test/chat/completions"),
                "test-model",
                "secret"));

        var result = await planner.CompleteAsync(
            new PlannerModelRequest("system", "payload"));

        Assert.Equal("""{"job":{}}""", result);
        Assert.Contains("test-model", requestBody);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal("secret", handler.LastAuthorizationParameter);
    }

    [Fact]
    public async Task CompleteAsync_ReportsHttpFailureWithoutParsingAsPlan()
    {
        var handler = new FakeHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("bad request")
                }));

        using var client = new HttpClient(handler);
        var planner = new ChatCompletionPlannerClient(
            client,
            new ChatCompletionTransportOptions(
                new Uri("https://example.test/chat/completions"),
                "test-model"));

        var error = await Assert.ThrowsAsync<PlannerTransportException>(() =>
            planner.CompleteAsync(
                new PlannerModelRequest("system", "payload")));

        Assert.Contains("400", error.Message);
        Assert.Equal("bad request", error.ResponseBody);
    }

    private sealed class FakeHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        public string? LastAuthorizationScheme { get; private set; }
        public string? LastAuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastAuthorizationScheme =
                request.Headers.Authorization?.Scheme;
            LastAuthorizationParameter =
                request.Headers.Authorization?.Parameter;

            return await responder(request);
        }
    }
}
