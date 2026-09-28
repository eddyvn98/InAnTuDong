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
                "nUp": null,
                "scaling": null,
                "placement": null,
                "crop": null,
                "booklet": null
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
            "columns": null,
            "gapMm": 2,
            "marginMm": 5,
            "border": false,
            "fit": "contain",
            "autoOrientation": true
          }
        - nUp.columns is optional. Leave it null for the deterministic standard grid.
        - Use nUp.columns only when the request clearly implies a different grid; for example 8 presentation slides on landscape paper should use columns=2 so the four rows have landscape-shaped cells.
        - If the user explicitly requests paper orientation, copy that orientation to paper.orientation and set nUp.autoOrientation=false.
        - If orientation is not explicit, set nUp.autoOrientation=true; deterministic code chooses the paper orientation.
        - If the user asks for space between N-up pages, set nUp.gapMm to the requested value, or 2 mm when space is requested without a size.
        - If the user asks for a border around each N-up page, set nUp.border=true.
        - N-up preserves selected source-page order in row-major order.
        - Duplex is independent from N-up: keep print.duplex exactly as requested.
        - Do not approximate unsupported pagesPerSheet values. Ask a concise clarification question.
        - Use ordinary grid layout only for repeated physical items such as labels/photos, not document pages-per-sheet.
        - Physical page scaling is represented with outputGroup.scaling and requires layout.fit="contain".
        - For "thu nhỏ nếu lớn hơn nhưng không phóng lớn", use:
          { "mode": "shrinkOnly", "percent": 100 }.
        - For explicit percentages, use:
          { "mode": "percent", "percent": N }.
          Examples: 80% -> 80; 125% -> 125; ratio 1:2 -> 50.
        - For "phóng tối đa nhưng vẫn giữ toàn bộ nội dung trong lề", use:
          { "mode": "maxFit", "percent": 100 }.
        - shrinkOnly and percent require trusted physical page sizes in the input source metadata.
          If the selected source does not provide physical page sizes, ask a concise clarification instead of guessing.
        - Percent scaling is centered and preserves source aspect/physical proportions.
          If a requested percent can exceed the printable target, keep the requested percent and add a warning that preview may show clipping.
        - Do not combine outputGroup.scaling with nUp in this slice.
        - Cover/fill/crop remains layout.fit="cover" with scaling=null.
        - General page placement is represented with outputGroup.placement and currently requires layout.mode="exactSize".
        - placement.margins contains leftMm/topMm/rightMm/bottomMm. For unspecified sides, keep the normal 5 mm default.
        - "chừa lề trái 20 mm" -> leftMm=20, other margins=5.
        - "chừa lề trên 15 mm và lề trái 25 mm" -> topMm=15, leftMm=25, other margins=5.
        - placement.anchor values are center, top, bottom, left, right, topLeft, topRight, bottomLeft, bottomRight.
        - "căn sát mép phải" -> anchor="right".
        - "căn xuống góc dưới bên phải" -> anchor="bottomRight".
        - placement.offsetXMm is positive to the right and negative to the left.
        - placement.offsetYMm is positive downward and negative upward.
        - "đưa nội dung lên trên 5 mm" -> offsetYMm=-5.
        - placement.shrinkToFit=true allows the physical placement rectangle to shrink proportionally when asymmetric margins reduce the available area.
        - General source crop is represented with outputGroup.crop.
        - "cắt bỏ phần trắng xung quanh" -> { "mode": "autoTrimWhite", "edgesMm": null, "whiteThreshold": 245 }.
        - "chỉ lấy phần giữa" -> { "mode": "centerToTargetAspect", "edgesMm": null, "whiteThreshold": 245 }.
        - Physical edge crop uses mode="edgesMm" and explicit edge millimetres.
          Example "cắt 10 mm ở mép trên" -> edgesMm.topMm=10 and other edge values=0.
        - EdgesMm crop requires trusted physical page sizes. If unavailable, ask instead of guessing.
        - Do not combine placement or crop with General N-up in this slice.
        - Do not combine general crop with physical scaling or Canvas in this slice.
        - Page placement may combine with crop.
        - Cover/fill remains a separate fit/crop-to-frame behavior and should not be used to represent explicit edge removal.
        - Booklet printing is represented with outputGroup.booklet.
        - Standard A4-to-A5 booklet:
          { "gutterMm": 4, "marginMm": 5 }.
        - A booklet always uses A4 paper in landscape execution and deterministic two-page imposition per side.
        - For booklet requests set print.duplex="shortEdge"; deterministic compilation also enforces the booklet short-edge duplex execution.
        - Booklet page order is not authored by the model. Keep selections in normal reading order; deterministic code pads to a multiple of four and reorders physical sides.
        - Example 8 logical pages become: 8,1 / 2,7 / 6,3 / 4,5.
        - Missing pages required to reach a multiple of four are virtual white pages; never invent source file paths for them.
        - "chừa mép giữa rộng hơn" should increase booklet.gutterMm; when no size is given use 10 mm.
        - sets=N + collate=true means N complete booklet copies.
        - Do not combine booklet with nUp, physical scaling, page placement, general source crop, or Canvas in this slice.
        - Booklet output is already imposed 2-up; never also set nUp.
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
