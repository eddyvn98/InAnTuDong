# M5 Built-in Workflows

## Goal

Provide common print tasks as deterministic built-in presets that create normal `PrintJobSpec` values.

Built-in workflows do not bypass validation, preview, policy or the Windows print adapter.

## First preset slice

### CCCD 1:1

- physical item size: 85.60 x 53.98 mm
- one source copy by default
- Contain fit
- high quality color
- preview required

This first version handles one scanned/photo side as one source. Front/back composition belongs to the later mixed-job slice.

### ID photo 3x4

- 30 x 40 mm
- 8 copies by default
- Cover fit
- cut marks
- high quality color
- preview required

### ID photo 4x6

- 40 x 60 mm
- 8 copies by default
- Cover fit
- cut marks
- high quality color
- preview required

### Label 40x60

- 40 x 60 mm
- 12 copies by default
- Contain fit
- cut marks
- standard quality color
- preview required

## Editing rule

After applying a built-in workflow, the generated job remains editable through the existing deterministic job editor.

The user can change copies, size, gap, margin, rotation, cut marks and fit, or save the edited result as a reusable local recipe.

## Architecture

```
built-in workflow
  -> PrintJobSpec
  -> PrintJobValidator
  -> deterministic layout
  -> preview
  -> policy/user approval
  -> Windows print adapter
```

No workflow is allowed to call the printer directly.


## CCCD front + back composition

The second M5 slice adds a mixed-source CCCD workflow:

- choose any two source pages as front and back
- pages may come from separate images/PDFs or two pages of the same PDF
- preserve source order: front first, back second
- render both on the same A4 page
- each card face remains 85.60 x 53.98 mm
- preview remains required
- printing still goes through the existing Windows spooler path

Mixed rendering is not CCCD-specific. Each layout placement now references its source index, and each source can reference a specific page within a PDF.


## Custom label / sticker sheets

The label workflow now accepts deterministic physical parameters:

- item width and height in millimetres
- requested copy count
- gap and A4 margin
- optional 90-degree rotation for better capacity
- cut marks on/off
- Contain or Cover fit

The requested physical label size is never reduced to force everything onto one sheet. If the requested quantity exceeds one A4 page, the normal layout engine creates additional A4 output pages.

Guardrails:

- dimensions must be positive
- copy count is limited to 1-1000 per job
- gap and margin cannot be negative
- at least one normal/rotated orientation must fit the A4 printable layout area
