# Desktop AI planner

PrintAI uses the locally installed and authenticated **Google Antigravity CLI (AGY)** as the primary natural-language planning transport.

No AI API key is required by PrintAI.

See `docs/ANTIGRAVITY_PLANNER.md` for the complete fast/deep design.

## Startup

On desktop startup PrintAI tries to locate `agy.exe` automatically.

If AGY is not found, natural-language planning is unavailable but deterministic preview/print workflows continue to work.

The UI exposes AGY readiness and an optional CLI-path retry field instead of endpoint/model/API-key credential fields.

## Configuration

Optional non-secret tuning:

```text
PRINTAI_AGY_PATH
PRINTAI_AGY_FAST_MODEL
PRINTAI_AGY_DEEP_MODEL
PRINTAI_AGY_FAST_EFFORT
PRINTAI_AGY_DEEP_EFFORT
PRINTAI_AGY_ESCALATE_BELOW
PRINTAI_AGY_TIMEOUT
```

Credentials are not PrintAI configuration. Authentication remains inside the user's Antigravity installation/session.

## Request flow

1. Select sources.
2. Enter a natural-language print request.
3. PrintAI inspects source metadata locally.
4. AGY fast/low-effort produces strict planner JSON.
5. PrintAI parses and validates it locally.
6. Invalid or low-confidence results escalate once to the configured deep model.
7. Source paths are rebound to the approved allowlist.
8. PrintPlan 2.0 compiles deterministically into PrintJobSpec 1.0 batches.
9. Existing Safe/Smart/Auto policy, preview and Windows print execution remain unchanged.

## Policy behavior

- Safe always requires preview.
- Smart requires preview for new/unapproved jobs.
- Auto may submit directly only if all policy gates pass.
- Planner questions stop direct execution.
- Warnings or confidence below policy thresholds require preview.
- An unverified printer profile requires preview.

## Source security boundary

AGY sees only the user request plus PrintAI-provided context.

Planner output may reference only selected/inspected sources. Unknown or rewritten paths are rejected before rendering or printing.

AGY never owns Windows print execution.

## Smart Collage

Smart Collage AI vision previously depended on a generic multimodal HTTP endpoint.

During the AGY migration, deterministic collage templates remain available while local-image handling through AGY is verified. PrintAI must not require a separate hidden API key for this feature.
