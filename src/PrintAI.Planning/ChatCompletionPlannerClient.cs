using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrintAI.Planning;

public sealed record ChatCompletionTransportOptions(
    Uri Endpoint,
    string Model,
    string? ApiKey = null);

public sealed class ChatCompletionPlannerClient(
    HttpClient httpClient,
    ChatCompletionTransportOptions options) :
    IPlannerModelClient,
    IMultimodalModelClient
{
    public Task<string> CompleteAsync(
        PlannerModelRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            [
                new { role = "system", content = request.SystemInstruction },
                new { role = "user", content = request.UserPayload }
            ],
            cancellationToken);

    public Task<string> CompleteMultimodalAsync(
        MultimodalModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var userContent = new List<object>
        {
            new { type = "text", text = request.UserText }
        };

        foreach (var image in request.Images.OrderBy(image => image.SourceIndex))
        {
            userContent.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = image.DataUrl,
                    detail = "low"
                }
            });
        }

        return SendAsync(
            [
                new { role = "system", content = request.SystemInstruction },
                new { role = "user", content = userContent.ToArray() }
            ],
            cancellationToken);
    }

    private async Task<string> SendAsync(
        object[] messages,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            message.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        message.Content = JsonContent.Create(new
        {
            model = options.Model,
            temperature = 0,
            messages
        });

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PlannerTransportException(
                $"Planner endpoint returned {(int)response.StatusCode}.",
                body);
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var content = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                throw new PlannerTransportException("Planner response content is empty.");

            return content;
        }
        catch (JsonException ex)
        {
            throw new PlannerTransportException(
                "Planner endpoint returned an unsupported response envelope.",
                body,
                ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new PlannerTransportException(
                "Planner endpoint returned an unsupported response envelope.",
                body,
                ex);
        }
    }
}

public sealed class PlannerTransportException : Exception
{
    public string? ResponseBody { get; }

    public PlannerTransportException(string message, string? responseBody = null)
        : base(message)
    {
        ResponseBody = responseBody;
    }

    public PlannerTransportException(
        string message,
        string? responseBody,
        Exception innerException)
        : base(message, innerException)
    {
        ResponseBody = responseBody;
    }
}
