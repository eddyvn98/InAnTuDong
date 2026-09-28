using System.Text.Json;
using System.Text.Json.Serialization;
using PrintAI.Domain;

namespace PrintAI.Planning;

public static class GeneralPrintPlanParser
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static GeneralPlanningOutcome Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        GeneralPlannerEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<GeneralPlannerEnvelope>(json, Options)
                ?? throw new PlanningFormatException("Planner returned an empty JSON value.");
        }
        catch (JsonException ex)
        {
            throw new PlanningFormatException(
                $"General planner JSON does not match the required schema: {ex.Message}",
                ex);
        }

        if (envelope.Plan is null)
            throw new PlanningFormatException("Planner JSON must contain plan.");

        if (envelope.Confidence is < 0 or > 1)
            throw new PlanningFormatException("confidence must be between 0 and 1.");

        var validation = PrintPlanValidator.Validate(envelope.Plan);
        if (!validation.IsValid)
        {
            var details = string.Join(
                "; ",
                validation.Errors.Select(error => $"{error.Code}: {error.Message}"));

            throw new PlanningFormatException($"Planned PrintPlan is invalid: {details}");
        }

        return new(
            Plan: envelope.Plan,
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

    private sealed record GeneralPlannerEnvelope(
        [property: JsonRequired] PrintPlan? Plan,
        [property: JsonRequired] double Confidence,
        IReadOnlyList<string>? Questions = null,
        IReadOnlyList<string>? Warnings = null);
}

public sealed record GeneralPlanningOutcome(
    PrintPlan Plan,
    double Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    string RawJson);
