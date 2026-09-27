using System.Text.Json;

namespace PrintAI.Planning;

public sealed class PrintPlanner(IPlannerModelClient modelClient)
{
    private const string SystemInstruction =
        """
        You are the planning layer for Print AI.
        Return JSON only. Do not return markdown.
        You may infer print intent, but never calculate device pixels or printer coordinates.
        Physical values are millimetres.
        Paper must not exceed A4 (210 x 297 mm).
        If a material choice is ambiguous, put a concise question in questions rather than guessing.
        The output must have exactly: job, confidence, questions, warnings.
        job.schemaVersion must be "1.0".
        Enum strings use camelCase.
        """;

    public async Task<PlanningOutcome> PlanAsync(
        PlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserRequest);

        if (request.Sources.Count == 0)
            throw new ArgumentException("At least one source is required.", nameof(request));

        var payload = JsonSerializer.Serialize(new
        {
            request = request.UserRequest,
            sources = request.Sources
        });

        var raw = await modelClient.CompleteAsync(
            new PlannerModelRequest(SystemInstruction, payload),
            cancellationToken);

        return PrintJobPlanParser.Parse(raw);
    }
}
