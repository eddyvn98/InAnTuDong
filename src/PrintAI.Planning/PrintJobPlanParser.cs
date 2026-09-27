using System.Text.Json;
using System.Text.Json.Serialization;
using PrintAI.Domain;

namespace PrintAI.Planning;

public static class PrintJobPlanParser
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static PlanningOutcome Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        PlannerEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PlannerEnvelope>(json, Options)
                ?? throw new PlanningFormatException("Planner returned an empty JSON value.");
        }
        catch (JsonException ex)
        {
            throw new PlanningFormatException(
                $"Planner JSON does not match the required schema: {ex.Message}",
                ex);
        }

        if (envelope.Job is null)
            throw new PlanningFormatException("Planner JSON must contain job.");

        if (envelope.Confidence is < 0 or > 1)
            throw new PlanningFormatException("confidence must be between 0 and 1.");

        var validation = PrintJobValidator.Validate(envelope.Job);
        if (!validation.IsValid)
        {
            var details = string.Join(
                "; ",
                validation.Errors.Select(e => $"{e.Code}: {e.Message}"));
            throw new PlanningFormatException($"Planned PrintJobSpec is invalid: {details}");
        }

        return new(
            Job: envelope.Job,
            Confidence: envelope.Confidence,
            Questions: envelope.Questions ?? [],
            Warnings: envelope.Warnings ?? [],
            RawJson: json);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }

    private sealed record PlannerEnvelope(
        [property: JsonRequired] PrintJobSpec? Job,
        [property: JsonRequired] double Confidence,
        IReadOnlyList<string>? Questions = null,
        IReadOnlyList<string>? Warnings = null);
}

public sealed class PlanningFormatException : Exception
{
    public PlanningFormatException(string message) : base(message)
    {
    }

    public PlanningFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
