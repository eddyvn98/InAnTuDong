using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrintAI.Planning;

public sealed class PlannerGatewayClient : IPlannerModelClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly bool _ownsClient;

    public PlannerGatewayClient(
        Uri endpoint,
        string? bearerToken = null)
        : this(new HttpClient(), endpoint, bearerToken, ownsClient: true)
    {
    }

    public PlannerGatewayClient(
        HttpClient httpClient,
        Uri endpoint,
        string? bearerToken = null)
        : this(httpClient, endpoint, bearerToken, ownsClient: false)
    {
    }

    private PlannerGatewayClient(
        HttpClient httpClient,
        Uri endpoint,
        string? bearerToken,
        bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "Planner gateway endpoint must be an absolute HTTP(S) URI.",
                nameof(endpoint));
        }

        _httpClient = httpClient;
        _endpoint = endpoint;
        _ownsClient = ownsClient;

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }

    public async Task<string> CompleteAsync(
        PlannerModelRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            _endpoint,
            new
            {
                systemInstruction = request.SystemInstruction,
                userPayload = request.UserPayload
            },
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PlannerGatewayException(
                $"Planner gateway returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                body);
        }

        if (string.IsNullOrWhiteSpace(body))
            throw new PlannerGatewayException("Planner gateway returned an empty body.");

        return body;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }
}

public sealed class PlannerGatewayException : Exception
{
    public string? ResponseBody { get; }

    public PlannerGatewayException(
        string message,
        string? responseBody = null)
        : base(message)
    {
        ResponseBody = responseBody;
    }
}

public sealed record PlannerGatewayConfiguration(
    Uri Endpoint,
    string? BearerToken)
{
    public static PlannerGatewayConfiguration? FromEnvironment()
    {
        var rawUrl = Environment.GetEnvironmentVariable("PRINTAI_PLANNER_URL");
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "PRINTAI_PLANNER_URL must be an absolute HTTP(S) URL.");
        }

        return new(
            endpoint,
            Environment.GetEnvironmentVariable("PRINTAI_PLANNER_TOKEN"));
    }
}
