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
        Leave at least 5 mm margins; every placed item must fit inside the printable area after margins and gaps.
        For full-page documents, preserve aspect ratio and fit them inside the printable area instead of requesting an exact 210 x 297 mm item.
        You only propose a validated PrintAI job. The local application inspects sources, validates layout, renders preview/PDF, and sends printing only after the user asks.
        If a material choice is ambiguous, put a concise question in questions rather than guessing.
        Source paths must be copied exactly from the provided sources. Never invent or rewrite a path.
        The output must have exactly: job, confidence, questions, warnings.
        job.schemaVersion must be "1.0".
        Enum strings use camelCase.
        """;

    public async Task<PlanningOutcome> PlanAsync(
        PlanningRequest request,
        CancellationToken cancellationToken = default,
        IProgress<PlannerProgressUpdate>? progress = null)
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
            new PlannerModelRequest(SystemInstruction, payload, progress),
            cancellationToken);

        return PrintJobPlanParser.Parse(raw);
    }
}
