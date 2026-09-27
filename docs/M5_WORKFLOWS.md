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
