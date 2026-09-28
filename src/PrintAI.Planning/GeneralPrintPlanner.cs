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

        Required JSON shape:
        {
          "plan": {
            "planName": "string",
            "sources": [
              { "path": "exact approved path", "pageCount": 1 }
            ],
            "outputGroups": [
              {
                "name": "string",
                "selections": [
                  {
                    "sourceIndex": 0,
                    "include": [
                      { "startPage": 1, "endPage": 1 }
                    ],
                    "exclude": [],
                    "parity": "all"
                  }
                ],
                "paper": {
                  "widthMm": 210,
                  "heightMm": 297,
                  "orientation": "portrait"
                },
                "layout": {
                  "mode": "exactSize",
                  "itemWidthMm": 200,
                  "itemHeightMm": 287,
                  "gapMm": 0,
                  "marginMm": 5,
                  "allowRotate": false,
                  "cutMarks": false,
                  "fit": "contain",
                  "canvas": null
                },
                "print": {
                  "colorMode": "color",
                  "quality": "standard",
                  "duplex": "off"
                },
                "sets": 1,
                "collate": true,
                "sequence": 0,
                "nUp": null
              }
            ],
            "policy": { "preview": "required" },
            "schemaVersion": "2.0"
          },
          "confidence": 0.95,
          "questions": [],
          "warnings": []
        }

        Rules for outputGroups:
        - Use one group when all selected pages share the same print settings.
        - Split groups when page ranges need different color, duplex, paper or layout.
        - For a complete multi-group document repeated N times, every group must use sets=N and collate=true.
          The deterministic compiler will interleave groups per complete set.
        - Do not mix collate=true and collate=false in one multi-group plan.
        - sequence values must be unique and start at 0 in intended execution order.
        - include/exclude page numbers are one-based and must stay inside source pageCount.
        - For ordinary document pages on A4, exactSize with a 5 mm margin and 200 x 287 mm content box is a safe default unless the request specifies another physical size.
        - General N-up is represented with outputGroup.nUp, not by guessing itemWidthMm/itemHeightMm.
        - Supported pagesPerSheet values are exactly: 2, 4, 6, 8, 9, 16.
        - For "N trang/tờ", "N-up", or "N slides per sheet", set nUp:
          {
            "pagesPerSheet": N,
            "gapMm": 2,
            "marginMm": 5,
            "border": false,
            "fit": "contain",
            "autoOrientation": true
          }
        - If the user explicitly requests paper orientation, copy that orientation to paper.orientation and set nUp.autoOrientation=false.
        - If orientation is not explicit, set nUp.autoOrientation=true; deterministic code chooses the paper orientation.
        - If the user asks for space between N-up pages, set nUp.gapMm to the requested value, or 2 mm when space is requested without a size.
        - If the user asks for a border around each N-up page, set nUp.border=true.
        - N-up preserves selected source-page order in row-major order.
        - Duplex is independent from N-up: keep print.duplex exactly as requested.
        - Do not approximate unsupported pagesPerSheet values. Ask a concise clarification question.
        - Use ordinary grid layout only for repeated physical items such as labels/photos, not document pages-per-sheet.
        - Do not use canvas layout in PrintPlan 2.0; Smart Collage owns canvas layouts.
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
