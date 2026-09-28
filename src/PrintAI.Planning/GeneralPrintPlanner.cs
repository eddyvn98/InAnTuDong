using System.Text.Json;

namespace PrintAI.Planning;

public sealed class GeneralPrintPlanner(IPlannerModelClient modelClient)
{
    private const string SystemInstruction =
        """
        You are the general print-intent planning layer for Print AI.
        Return JSON only. Do not return markdown.
        The output must have exactly: plan, confidence, questions, warnings.
        plan.schemaVersion must be "2.0".
        Source paths and source pageCount values must be copied exactly from the provided sources.
        SourceIndex is zero-based. Page numbers inside include/exclude ranges are one-based and inclusive.
        Use one or more outputGroups when different pages need different color, duplex, paper, layout, or execution order.
        sets means repeated complete sets. collate=true means each complete selected sequence is repeated as a set.
        Do not use pricing, customer, inventory, delivery, finishing, or any non-print workflow.
        Do not calculate printer pixels or driver coordinates. Physical values are millimetres.
        Paper in the current execution layer cannot exceed A4 (210 x 297 mm).
        If the request is materially ambiguous, ask a concise question instead of guessing.
        Enum strings use camelCase.
        """;

    public async Task<GeneralPlanningOutcome> PlanAsync(
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

        return GeneralPrintPlanParser.Parse(raw);
    }
}
