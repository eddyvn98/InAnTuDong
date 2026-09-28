# Smart Collage / Layout V2

## Goal

Move PrintAI from grid-only placement to an AI-ready collage canvas while preserving the deterministic print pipeline.

The target experience is:

```text
select 3 photos
  -> choose 4x6
  -> AI analyses the photos
  -> AI chooses/adapts collage templates
  -> deterministic renderer validates and renders alternatives
  -> user chooses one
  -> print
```

Auto-grid remains a fast fallback. Smart Collage is a separate capability.

## Architecture

### 1. Canvas scene graph

A canvas contains explicit placements. Every placement owns its geometry instead of inheriting one global item width/height.

A placement contains:

- source index
- x/y in millimetres
- width/height in millimetres
- rotation
- z-index
- shape/mask
- fit mode
- image transform:
  - scale
  - normalized offset X/Y

Initial masks:

- rectangle
- rounded rectangle
- ellipse
- circle

Planned masks:

- triangle
- hexagon
- polygon
- SVG path

### 2. Backward compatibility

PrintJobSpec 1.0 grid/exact-size remains supported.

Canvas layout is optional. When absent, LayoutEngine behaves exactly as before. When present, the canvas renderer is authoritative.

This avoids forcing migration of existing saved jobs and keeps auto-grid as a safe fallback.

### 3. Template library

Templates are deterministic data, not generated pixels.

Initial three-photo templates:

1. hero-left-two-right
2. hero-top-two-bottom
3. hero-right-two-left
4. three-rounded-columns
5. center-circle-two-sides
6. overlapping-cards
7. diagonal-cards
8. large-center-corners
9. stacked-soft
10. asymmetrical-editorial

Templates may define frame geometry, shape, rotation and z-order. Image transforms are separate so AI can reframe a photo without moving the frame.

### 4. AI responsibilities

AI does not print and does not directly draw arbitrary pixels.

AI may:

- classify photo orientation
- identify important subjects/faces/salient regions
- choose suitable templates
- choose source-to-frame assignment
- adjust crop scale and offset
- request bounded frame mutations
- rank or explain alternatives

The deterministic layer must:

- validate all geometry
- reject placements outside the paper
- reject invalid source references
- clamp or reject unsupported transforms
- render masks/transforms
- produce the final PrintJobSpec/canvas state
- submit the exact paper size to the printer

### 5. Candidate generation pipeline

```text
source analysis
 -> template shortlist
 -> source assignment
 -> image transforms
 -> deterministic validation
 -> render candidates
 -> score
 -> top alternatives
 -> user selection
```

The first implementation milestone does not require an AI provider. It builds the scene graph, renderer and template library first so AI can be connected to a stable contract later.

## Milestones

### V2.1 - scene graph foundation

- optional CanvasLayoutSpec
- explicit placements
- z-index
- per-placement fit
- image scale and offset
- rectangle / rounded rectangle / ellipse / circle masks
- deterministic validation
- renderer tests

### V2.2 - template library

- at least 10 three-photo 4x6 templates
- portrait and landscape variants where appropriate
- template preview generation
- source permutation support
- template metadata/style tags

### V2.3 - smart candidate generator

- generate candidate set from selected photos
- deterministic heuristic ranking
- top four preview gallery
- regenerate alternatives
- preserve selected candidate as active print job

### V2.4 - AI photo analysis

Initial implementation complete:

- OpenAI-compatible multimodal vision adapter reusing the existing endpoint/model/API-key session
- three low-resolution source thumbnails sent in one multimodal request
- per-source orientation, importance and normalized focal-point analysis
- strict template-selection JSON schema
- AI source-to-frame assignment
- bounded scale and normalized X/Y crop offsets
- deterministic validation of every AI proposal before rendering
- up to four ranked AI candidates
- automatic four-template fallback when AI is unavailable, malformed or non-vision-capable

Current limitation: there is no local face detector yet. Face/subject awareness is inferred by the configured vision model and therefore remains advisory. Preview remains required before printing.

### V2.5 - manual refinement

- select frame
- drag image under mask
- zoom image
- move/resize/rotate frame
- undo/redo

Manual refinement is optional for the main workflow; the primary experience remains AI-first.

## Acceptance criteria for V2.1

1. Existing schema 1.0 grid jobs render unchanged.
2. A canvas job can place three sources at different sizes and positions.
3. Z-index controls overlap order.
4. Rectangle, rounded rectangle, ellipse and circle masks clip source pixels.
5. Per-placement scale and offsets reframe the source without changing frame geometry.
6. Invalid source indices and out-of-paper placements are rejected.
7. Canvas preview respects 4x6 paper dimensions.
8. Tests cover masks, transforms, overlap order and bounds.
