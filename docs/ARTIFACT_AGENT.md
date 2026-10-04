# General Artifact Agent

## Goal

The local AI-first UI stays minimal:

```text
files/thumbnails + one natural-language request + preview + print
```

The app must not force every request into a print schema.

A user may ask to:

- reformat or restructure an Office document;
- change the underlying content of a document;
- modify an image before layout/printing;
- modify a PDF before printing;
- create a native/editable artifact;
- perform any of the above and then print the result.

## Request routing

The same chat request is classified by AGY into one of three routes:

- `print` — no underlying source edit is needed; use existing PrintPlan / PrintJobSpec execution.
- `artifact` — edit or create artifact files, then return them to the source gallery for preview/follow-up.
- `artifactThenPrint` — edit/create first, then pass the generated outputs into the existing deterministic print planner.

This router exists to choose the execution family, not to enumerate every possible document operation.

## Artifact workspace boundary

Artifact work runs inside a per-task directory below the process-scoped PrintAI workspace.

```text
artifact-tasks/<task-id>/
  input/    copies of user-selected original files
  output/   final deliverables created by AGY
```

Rules:

1. Uploaded Office originals are retained. Their converted PDF is only a preview/print representation.
2. AGY receives copies of selected originals, never arbitrary filesystem paths.
3. AGY is instructed to work only inside the task workspace.
4. AGY runs with the CLI sandbox enabled.
5. Final deliverables must be written under `output/`.
6. PrintAI imports supported output files back into the session.
7. Office outputs are converted to PDF only for preview/printing while their native originals remain available for later edits.
8. AGY must not call a printer or spooler.
9. All actual printing still goes through validated PrintPlan / PrintJobSpec and the existing deterministic printer path.

## Why this is different from the old planner

Before this change, the primary AGY client explicitly instructed the model not to call tools or edit files, and every selected-file request was pushed into PrintPlan 2.0.

That is still correct for pure printing requests, but it prevents general document preparation.

PrintPlan is now an execution capability for printing rather than the universal schema for every user request.

## Current implementation slice

Implemented on `feature/general-artifact-agent`:

- preserve original Office files after PDF conversion;
- route local chat requests into print / artifact / artifactThenPrint;
- run artifact tasks in a per-task workspace;
- re-import generated outputs into the existing source gallery;
- automatically continue into print planning for artifactThenPrint;
- keep the existing minimal UI instead of adding workflow controls;
- keep deterministic preview and printer execution unchanged.

## Verification still required

- CI build/test for the branch;
- target-machine AGY verification that sandboxed headless tool use can create/edit files under the supplied working directory;
- real DOCX/XLSX/PPTX edit cases with LibreOffice/available local tools;
- image/PDF manipulation cases;
- confirm AGY never blocks waiting for interactive tool approval in headless mode.

If headless AGY tool approval requires an additional CLI flag, add that flag only after verifying the installed CLI contract on the target machine. Do not weaken the workspace boundary or disable print safety to work around it.
