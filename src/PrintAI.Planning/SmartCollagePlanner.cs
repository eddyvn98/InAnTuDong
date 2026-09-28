using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintAI.Planning;

public sealed record SmartCollageSourceAnalysis(
    int SourceIndex,
    string Orientation,
    double Importance,
    double SubjectX,
    double SubjectY,
    string? Notes = null);

public sealed record SmartCollageFrameProposal(
    int FrameIndex,
    int SourceIndex,
    double Scale,
    double OffsetX,
    double OffsetY);

public sealed record SmartCollageCandidateProposal(
    string TemplateId,
    double Confidence,
    string Reason,
    IReadOnlyList<SmartCollageFrameProposal> Frames);

public sealed record SmartCollagePlan(
    IReadOnlyList<SmartCollageSourceAnalysis> Analysis,
    IReadOnlyList<SmartCollageCandidateProposal> Candidates,
    string RawJson);

public sealed class SmartCollagePlanner(IMultimodalModelClient modelClient)
{
    private const string SystemInstruction =
        """
        You are the visual layout planner for Print AI Smart Collage.
        Return JSON only. Do not return markdown.
        You receive exactly three source images in sourceIndex order 0, 1, 2.
        You may analyse composition, important subjects, faces and safe crop direction.
        You do not draw pixels and you do not invent templates.
        Choose only template IDs listed in the user payload.
        Return exactly: analysis, candidates.

        analysis must contain exactly one entry for each sourceIndex 0,1,2:
        - orientation: portrait, landscape or square
        - importance: 0..1
        - subjectX and subjectY: estimated normalized focal point, 0..1
        - notes: concise optional text

        candidates must contain 1..4 visually distinct proposals ordered best first.
        Each candidate:
        - templateId: one allowed template ID
        - confidence: 0..1
        - reason: concise
        - frames: exactly 3 entries
        Each frame entry:
        - frameIndex: 0,1,2 exactly once
        - sourceIndex: 0,1,2 exactly once
        - scale: 1.0..3.0. Use >1 only when useful for subject framing.
        - offsetX and offsetY: -0.8..0.8. Positive offset moves crop focus right/down.

        Prefer not to cut faces or important subjects.
        Use a hero-style template when one photo is clearly stronger.
        Use balanced/shape templates when photos have similar importance.
        Do not output any coordinates in millimetres.
        """;

    public async Task<SmartCollagePlan> PlanAsync(
        IReadOnlyList<MultimodalImage> images,
        IReadOnlyList<string> allowedTemplateIds,
        string? userInstruction = null,
        CancellationToken cancellationToken = default)
    {
        if (images.Count != 3)
            throw new ArgumentException("Smart Collage vision currently requires exactly three images.");

        if (allowedTemplateIds.Count == 0)
            throw new ArgumentException("At least one collage template is required.");

        var payload = JsonSerializer.Serialize(new
        {
            instruction = string.IsNullOrWhiteSpace(userInstruction)
                ? "Create attractive 4x6 portrait collage alternatives."
                : userInstruction,
            templates = allowedTemplateIds,
            sources = images
                .OrderBy(image => image.SourceIndex)
                .Select(image => new
                {
                    image.SourceIndex,
                    image.PixelWidth,
                    image.PixelHeight
                })
        });

        var raw = await modelClient.CompleteMultimodalAsync(
            new MultimodalModelRequest(SystemInstruction, payload, images),
            cancellationToken);

        return Parse(raw, allowedTemplateIds);
    }

    public static SmartCollagePlan Parse(
        string json,
        IReadOnlyList<string> allowedTemplateIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        SmartCollageEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SmartCollageEnvelope>(
                json,
                CreateOptions())
                ?? throw new PlanningFormatException("Smart Collage AI returned empty JSON.");
        }
        catch (JsonException ex)
        {
            throw new PlanningFormatException(
                $"Smart Collage JSON does not match the required schema: {ex.Message}",
                ex);
        }

        Validate(envelope, allowedTemplateIds);

        return new(
            envelope.Analysis!,
            envelope.Candidates!,
            json);
    }

    private static void Validate(
        SmartCollageEnvelope envelope,
        IReadOnlyList<string> allowedTemplateIds)
    {
        if (envelope.Analysis is null || envelope.Analysis.Count != 3)
            throw new PlanningFormatException("analysis must contain exactly three sources.");

        if (envelope.Analysis.Select(item => item.SourceIndex).Order().SequenceEqual([0, 1, 2]) == false)
            throw new PlanningFormatException("analysis sourceIndex values must be exactly 0,1,2.");

        foreach (var item in envelope.Analysis)
        {
            if (item.Orientation is not ("portrait" or "landscape" or "square"))
                throw new PlanningFormatException("analysis orientation must be portrait, landscape or square.");

            if (item.Importance is < 0 or > 1 ||
                item.SubjectX is < 0 or > 1 ||
                item.SubjectY is < 0 or > 1)
            {
                throw new PlanningFormatException("analysis normalized values must be between 0 and 1.");
            }
        }

        if (envelope.Candidates is null || envelope.Candidates.Count is < 1 or > 4)
            throw new PlanningFormatException("candidates must contain between one and four proposals.");

        var allowed = allowedTemplateIds.ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in envelope.Candidates)
        {
            if (!allowed.Contains(candidate.TemplateId))
                throw new PlanningFormatException($"Unknown collage template: {candidate.TemplateId}.");

            if (candidate.Confidence is < 0 or > 1)
                throw new PlanningFormatException("candidate confidence must be between 0 and 1.");

            if (candidate.Frames.Count != 3)
                throw new PlanningFormatException("Each candidate must contain exactly three frames.");

            if (!candidate.Frames.Select(frame => frame.FrameIndex).Order().SequenceEqual([0, 1, 2]))
                throw new PlanningFormatException("frameIndex values must be exactly 0,1,2.");

            if (!candidate.Frames.Select(frame => frame.SourceIndex).Order().SequenceEqual([0, 1, 2]))
                throw new PlanningFormatException("Each sourceIndex 0,1,2 must be used exactly once.");

            foreach (var frame in candidate.Frames)
            {
                if (frame.Scale is < 1 or > 3)
                    throw new PlanningFormatException("frame scale must be between 1 and 3.");

                if (frame.OffsetX is < -0.8 or > 0.8 ||
                    frame.OffsetY is < -0.8 or > 0.8)
                {
                    throw new PlanningFormatException("frame offsets must be between -0.8 and 0.8.");
                }
            }
        }
    }

    private static JsonSerializerOptions CreateOptions() =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

    private sealed record SmartCollageEnvelope(
        [property: JsonRequired] IReadOnlyList<SmartCollageSourceAnalysis>? Analysis,
        [property: JsonRequired] IReadOnlyList<SmartCollageCandidateProposal>? Candidates);
}
