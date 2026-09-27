# Desktop AI planner configuration

The desktop AI planner uses a configurable chat-completions-compatible HTTP endpoint.

The deterministic printing engine does not depend on any AI provider SDK.

## Configure in the app

In the Desktop **AI planner** section, enter:

- Endpoint
- Model
- API key, if the endpoint requires one

The API key entered in the UI is kept only for the current process. It is not written to job history.

## Configure with environment variables

The desktop app also reads:

```text
PRINTAI_AI_ENDPOINT
PRINTAI_AI_MODEL
PRINTAI_AI_API_KEY
```

`PRINTAI_AI_API_KEY` is optional for local/no-auth endpoints.

## Request flow

1. Select a JPG, PNG or PDF source/page.
2. Enter a natural-language request.
3. Choose Safe, Smart or Auto.
4. Click **AI lập kế hoạch**.
5. The model returns a strict versioned JSON proposal.
6. PrintAI rejects unknown schema fields, invalid values and unapproved source paths.
7. PrintAI runs deterministic validation and policy evaluation.
8. The desktop renders the resulting A4 output pages.
9. The user may edit physical settings before printing.

## Policy behavior

- Safe always requires preview.
- Smart requires preview for new/unapproved jobs.
- Auto may submit directly only if all policy gates pass.
- Planner questions stop direct execution.
- Warnings or confidence below 0.90 require preview.
- An unverified printer profile requires preview.

The Epson L3310 profile in this repository is physically verified.

## Source security boundary

The planner is given inspected metadata and the selected source path. It must copy the path exactly.

A planner response that references another local path is rejected. The AI cannot use the planner JSON to make PrintAI read an arbitrary file.

## History

PrintAI keeps up to 100 local history entries under the user's LocalAppData PrintAI folder.

History records planning/printing status and job metadata. It does not persist the AI API key.
