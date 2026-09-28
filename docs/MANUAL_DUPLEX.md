# Manual Duplex Printing

## Status

Planned release-blocking correctness work for M6.

The current `PrintJobSpec` already carries `DuplexMode.Off`, `LongEdge` and `ShortEdge`, and the Windows printer probe already exposes `CanDuplex`. However, the current spooler path submits one rendered A4 PNG per `PrintDocument` and does not execute the duplex intent.

A job that requests duplex must never silently print as simplex.

This document defines the implementation for printers that cannot automatically duplex, including the current simplex hardware path.

## Product goal

When the user asks for two-sided printing on a printer with `CanDuplex=false`, Print AI should execute a guided two-pass manual-duplex workflow.

The user should not need to understand odd/even pages, reverse order, driver settings, or page reordering.

The normal experience is:

1. choose or request two-sided printing,
2. print all front sides,
3. keep the printed stack together,
4. reinsert it as instructed,
5. confirm once,
6. print all back sides.

The app owns ordering, rotation, resume state and capability checks.

## Terminology

- **Duplex intent**: the user-facing `PrintJobSpec.Print.Duplex` value.
- **Automatic duplex**: the printer/driver performs both sides in one printer operation.
- **Manual duplex**: Print AI performs two simplex passes with a user reinsert step.
- **Physical sheet**: one piece of paper with an optional front and back output page.
- **Front pass**: all front sides sent in one deterministic batch.
- **Back pass**: all back sides sent in one deterministic batch after reinsertion.
- **Reinsert profile**: printer-specific rules that describe stack order, feed orientation and back-side rotation.

## PrintJobSpec semantics

Do not change schema 1.0 for this feature.

`DuplexMode` remains printer-neutral intent:

- `Off` — one-sided output.
- `LongEdge` — two-sided output bound/flipped on the long edge.
- `ShortEdge` — two-sided output bound/flipped on the short edge.

Execution is selected after the printer is known:

```text
Duplex Off
  -> simplex execution

Duplex LongEdge/ShortEdge
  -> printer CanDuplex?
       yes -> automatic duplex adapter
       no  -> manual duplex planner
```

A simplex printer is therefore not an error merely because duplex was requested. The runtime should fall back to manual duplex when a verified/manual-capable reinsert path exists.

The runtime must never silently replace `LongEdge` or `ShortEdge` with `Off`.

## Physical-sheet model

Manual duplex must be sheet-based rather than odd/even-page based.

Example: 5 output pages.

```text
Sheet 1
  Front = output page 1
  Back  = output page 2

Sheet 2
  Front = output page 3
  Back  = output page 4

Sheet 3
  Front = output page 5
  Back  = blank
```

Suggested shared records:

```csharp
public sealed record DuplexSheet(
    int SheetIndex,
    int FrontOutputPageIndex,
    int? BackOutputPageIndex);

public sealed record ManualDuplexPlan(
    DuplexMode Mode,
    IReadOnlyList<DuplexSheet> Sheets,
    IReadOnlyList<PrintSideInstruction> FrontPass,
    IReadOnlyList<PrintSideInstruction> BackPass);

public sealed record PrintSideInstruction(
    int SheetIndex,
    int OutputPageIndex,
    int RotationDegrees);
```

The planner should operate on already-renderable A4 output page indexes. It must not depend on source file type.

## Multiple copies

Copies must be expanded into physical sheet instances before pass ordering is calculated.

For a six-page document with two copies:

```text
Copy 1: 1<->2, 3<->4, 5<->6
Copy 2: 1<->2, 3<->4, 5<->6
```

The preferred UX performs one front pass across the full batch, one reinsert action, then one back pass across the full batch.

The planner must preserve copy boundaries and page pairing even if the back pass is physically reversed by the printer profile.

## Printer execution capability

The existing `PrinterCapabilitySnapshot.CanDuplex` remains the source for advertised automatic-duplex capability.

Add a separate manual-duplex capability/profile. Do not treat `CanDuplex=false` as meaning that two-sided output is impossible.

Suggested profile shape:

```csharp
public sealed record ManualDuplexProfile(
    bool IsVerified,
    ManualDuplexBackOrder BackOrder,
    int LongEdgeBackRotationDegrees,
    int ShortEdgeBackRotationDegrees,
    string ReinsertInstructionId);
```

The exact shape may be adjusted to fit the existing `PrinterDeviceProfile`, but the following facts must be representable:

- whether manual duplex has been physically verified for this printer/profile,
- whether the back pass must run forward or reversed,
- how the stack must be reinserted,
- whether the back raster must be rotated for long-edge printing,
- whether the back raster must be rotated for short-edge printing.

Do not infer verification from printer name.

## Calibration

Manual duplex behavior depends on real paper transport and must be calibrated per printer/driver/profile.

Add a calibration flow that prints an orientation test and records the reinsert result.

Minimum calibration:

1. print a front test page with obvious TOP/BOTTOM and FRONT marks,
2. ask the user to reinsert the sheet using a shown orientation,
3. print a back test page with obvious TOP/BOTTOM and BACK marks,
4. determine/save the required stack order and rotation,
5. mark the manual-duplex profile verified only after the user confirms the physical result.

The first implementation may use a small finite set of reinsert choices rather than computer vision.

Changing printer driver, relevant paper/feed settings, or a physically different printer should require re-verification.

## Windows spooler changes

The current `WindowsSpoolerPrinter.SubmitA4Png` submits one page and sets `HasMorePages=false`.

Add a multi-page batch API, for example:

```csharp
SubmitA4Pages(
    string printerName,
    IReadOnlyList<PrintableA4Page> pages,
    PrinterDeviceProfile? profile = null,
    short copies = 1)
```

One call should create one `PrintDocument` and emit all provided pages through successive `PrintPage` callbacks.

This API is required for:

- a single front pass,
- a single back pass,
- future automatic-duplex execution.

Do not implement manual duplex by firing one unrelated spooler job per page.

## Manual-duplex state machine

Desktop orchestration should be explicit and persistable.

```text
Ready
  -> PrintingFront
  -> WaitingForReinsert
  -> PrintingBack
  -> Completed
```

Error/cancel states:

```text
PrintingFront -> Failed
WaitingForReinsert -> Cancelled
PrintingBack -> Failed
```

A pending job must contain enough information to resume the back pass without printing the front pass again.

Suggested persisted state:

```csharp
public sealed record PendingManualDuplexJob(
    string Id,
    string PrinterName,
    DuplexMode Mode,
    DateTimeOffset CreatedAt,
    bool FrontCompleted,
    int SheetCount,
    IReadOnlyList<PrintSideInstruction> BackPass,
    string JobFingerprint);
```

Do not persist document content inside the state record. Persist approved references/work artifacts using the same privacy boundary as existing local job data.

## Desktop UX

### Print settings

Expose:

```text
Printing
  ( ) One-sided
  ( ) Two-sided - book / long edge
  ( ) Two-sided - calendar / short edge
```

For a simplex printer, the two-sided options remain available and show that Print AI will use manual duplex.

Avoid exposing "odd pages" and "even pages" as the primary workflow.

### Pre-print summary

Before submission show the physical consequence:

```text
Printer: <selected printer>
Document: 6 output pages
Paper: 3 A4 sheets
Mode: Two-sided manual - long edge

Steps:
1. Print 3 front sides
2. Reinsert the stack once
3. Print 3 back sides
```

### Reinsert screen

After the front pass, replace the ordinary status message with a prominent blocking step.

Requirements:

- say that the front pass completed,
- show the verified printer-specific reinsert illustration/instruction,
- tell the user to keep the stack order unchanged unless the profile explicitly says otherwise,
- provide one primary action: `Paper reinserted - print backs`,
- provide cancel,
- do not automatically start the back pass.

### Resume

On startup, if a pending manual-duplex job exists, surface:

```text
A two-sided job is waiting for the back pass.
[Continue back pass] [Cancel job]
```

Never restart the front pass automatically.

## AI planner behavior

The AI planner continues to express intent through `PrintJobSpec.Print.Duplex`.

Examples:

- "in 2 mặt kiểu sách" -> `longEdge`
- "in 2 mặt lật cạnh ngắn" -> `shortEdge`
- no duplex request -> `off`

The model does not choose automatic versus manual duplex.

The deterministic runtime resolves that from printer capability/profile data.

## Validation and policy

Add execution validation after printer selection.

Rules:

1. `Duplex=Off` is always eligible for the existing simplex path.
2. `Duplex!=Off && CanDuplex=true` may use automatic duplex when the Windows adapter supports it.
3. `Duplex!=Off && CanDuplex=false` should use manual duplex.
4. If manual duplex has no verified reinsert profile, require preview/calibration/explicit user guidance; do not silently print simplex.
5. A duplex request with only one output page may degrade to one physical front side only, but the UI should describe that no back page exists.

Manual duplex is user-interactive, so it is never fully unattended direct-print across both passes.

## Rendering rules

Front and back sides use the same deterministic A4 renderer and calibrated scale/offset as simplex output.

Back-side transforms belong to the manual-duplex execution layer, not to source layout.

The planner may request long-edge or short-edge binding, but only deterministic code applies the required back-side rotation from the verified printer profile.

A blank back side for an odd page count is not sent as a fake source page unless the physical pass ordering requires an actual blank feed. Prefer a plan representation that can distinguish "no back image" from a rendered blank page.

## Failure handling

### Front pass fails

- stop,
- do not enter `WaitingForReinsert`,
- record the spooler error,
- allow the user to restart the job.

### Back pass fails

- preserve `FrontCompleted=true`,
- keep the pending back-pass plan,
- allow retry of the back pass,
- never silently reprint fronts.

### App exits while waiting

- persist the pending state,
- restore the reinsert screen on next startup.

### Printer changes between passes

Block the back pass until the user selects the original printer or explicitly restarts the duplex job. The physical stack assumptions belong to the original printer/profile.

## Implementation slices

### Slice A - deterministic planner

Add:

- physical-sheet pairing,
- odd output-page handling,
- copy expansion,
- front/back pass ordering,
- long-edge/short-edge plan metadata.

Tests are platform-independent and belong in `PrintAI.Tests`.

### Slice B - multi-page Windows submission

Add a multi-page `PrintDocument` submission API.

Windows tests should verify page enumeration/order without requiring real physical printing where possible.

Keep the existing single-page API as a compatibility wrapper if useful.

### Slice C - desktop orchestration

Add:

- duplex controls,
- capability resolution,
- state machine,
- front-pass submission,
- reinsert screen,
- back-pass submission,
- local pending-state persistence/resume.

### Slice D - calibration/profile

Add:

- manual-duplex profile fields,
- calibration pages,
- profile verification persistence,
- reinsert instruction rendering.

### Slice E - release regression

Add regression coverage for:

- 1 page,
- 2 pages,
- 5 pages,
- 6 pages,
- multiple copies,
- long edge,
- short edge,
- forward and reverse back-pass profiles,
- restart while waiting,
- back-pass retry,
- printer mismatch between passes.

## Suggested file impact

Likely new files:

```text
src/PrintAI.Domain/
  ManualDuplexPlan.cs

src/PrintAI.Windows.Printing/
  ManualDuplexProfile.cs
  ManualDuplexPlanner.cs

src/PrintAI.Desktop/
  DesktopSession.Duplex.cs
  PendingManualDuplexStore.cs
```

Likely modified files:

```text
src/PrintAI.Windows.Printing/WindowsSpoolerPrinter.cs
src/PrintAI.Windows.Printing/PrinterDeviceProfile.cs
src/PrintAI.Windows.Printing/PrinterProfileCatalog.cs
src/PrintAI.Desktop/DesktopModels.cs
src/PrintAI.Desktop/DesktopSession.Printing.cs
src/PrintAI.Desktop/ui/index.html
tests/PrintAI.Tests/*
tests/PrintAI.Windows.Tests/*
```

Keep source files focused and reasonably small per `AGENTS.md`.

## Acceptance criteria

The implementation is not complete until all of the following are true:

- duplex intent is visible/editable in the desktop UI,
- a simplex printer does not silently ignore duplex intent,
- five output pages map to three physical sheets correctly,
- multiple copies preserve front/back pairing,
- front pass is one deterministic multi-page batch,
- the app stops and requires user confirmation before the back pass,
- back-pass order/rotation comes from the selected printer's manual-duplex profile,
- restart while waiting can resume backs without reprinting fronts,
- a back-pass failure can retry backs without reprinting fronts,
- changing printer between passes is detected,
- automated planner/state tests are green,
- real paper calibration is recorded for the target printer before claiming verified manual duplex,
- `docs/PROGRESS.md` records the physical verification result.

## Non-goals for this slice

- booklet imposition,
- N-up book signatures,
- arbitrary paper sizes above A4,
- automatic sheet flipping hardware control on a simplex printer,
- Epson UI automation,
- computer-vision detection of paper orientation,
- changing PrintJobSpec schema version.

## Release rule

A build must not be tagged as release-ready while it can accept `duplex != off` and then silently execute simplex output.

Until automatic or manual duplex execution exists, the app must block or explicitly require a supported manual workflow for duplex jobs.
