using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PrintAI.Planning;
using Xunit;

namespace PrintAI.Tests;

public sealed class PlannerGatewayClientTests
{
    [Fact]
    public async Task CompleteAsync_PostsPrintAiGatewayContract()
    {
        var handler = new CaptureHandler(
            HttpStatusCode.OK,
            """
            {"job":{"jobName":"x","sources":[],"paper":{"widthMm":210,"heightMm":297,"orientation":"portrait"},"layout":{"mode":"grid","itemWidthMm":10,"itemHeightMm":10},"print":{},"policy":{},"schemaVersion":"1.0"},"confidence":0.9,"questions":[],"warnings":[]}
            """);

        using var http = new HttpClient(handler);
        using var client = new PlannerGatewayClient(
            http,
            new Uri("https://planner.example/api/plan"),
            "secret-token");

        var body = await client.CompleteAsync(
            new PlannerModelRequest("system", "payload"));

        using var responseJson = JsonDocument.Parse(body);
        Assert.Equal(
            0.9,
            responseJson.RootElement.GetProperty("confidence").GetDouble(),
            2);
        Assert.Equal(
            "Bearer secret-token",
            handler.Authorization);

        using var requestJson = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(
            "system",
            requestJson.RootElement.GetProperty("systemInstruction").GetString());
        Assert.Equal(
            "payload",
            requestJson.RootElement.GetProperty("userPayload").GetString());
    }

    [Fact]
    public async Task CompleteAsync_ThrowsOnGatewayFailure()
    {
        var handler = new CaptureHandler(
            HttpStatusCode.BadGateway,
            "upstream unavailable");

        using var http = new HttpClient(handler);
        using var client = new PlannerGatewayClient(
            http,
            new Uri("https://planner.example/api/plan"));

        var error = await Assert.ThrowsAsync<PlannerGatewayException>(() =>
            client.CompleteAsync(new PlannerModelRequest("system", "payload")));

        Assert.Contains("502", error.Message);
        Assert.Equal("upstream unavailable", error.ResponseBody);
    }

    private sealed class CaptureHandler(
        HttpStatusCode status,
        string responseBody) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
