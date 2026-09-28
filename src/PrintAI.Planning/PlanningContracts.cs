using PrintAI.Domain;

namespace PrintAI.Planning;

public sealed record PlanningSource(
    string Path,
    string Kind,
    int? PixelWidth = null,
    int? PixelHeight = null,
    int? PageCount = null,
    IReadOnlyList<SourcePageSizeSpec>? Pages = null);

public sealed record PlanningRequest(
    string UserRequest,
    IReadOnlyList<PlanningSource> Sources);

public sealed record PlannerModelRequest(
    string SystemInstruction,
    string UserPayload);

public interface IPlannerModelClient
{
    Task<string> CompleteAsync(
        PlannerModelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PlanningOutcome(
    PrintJobSpec Job,
    double Confidence,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings,
    string RawJson);
