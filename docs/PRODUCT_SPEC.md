# Product Specification

## Product

Print AI is a Windows-first assistant that accepts local files/folders plus a natural-language print request and converts them into accurate, reviewable print jobs without requiring the user to operate the printer manufacturer's app.

## Primary interaction

1. User provides one or many file paths, a folder, drag/drop files, or scanner input.
2. User describes the outcome in natural language.
3. AI converts intent to a structured `PrintJobSpec`.
4. Deterministic code validates sizes/capabilities and calculates layout.
5. The system previews uncertain/risky jobs.
6. A Windows agent submits the job through the installed driver/spooler.

Examples:

- "In file này A4 1 bản."
- "Mỗi ảnh 4x6 cm, 2 bản, xếp tiết kiệm giấy, có đường cắt."
- "Ghép 2 mặt CCCD thành 3 bộ trên A4."
- "Scan 5 tờ thành một PDF rồi in 2 bản."

## V1 hardware scope

Primary profile: Epson L3310.

Required:

- A4 maximum paper size
- color/document/photo printing
- scanning
- multiple copies
- portrait/landscape
- fit / contain / cover / exact physical size
- spacing and cut marks
- multi-item A4 layout
- guided two-sided printing on simplex printers through manual duplex

Architecture must allow other printers later.

## Functional requirements

### Input and inspection

Initial source types: JPG/JPEG, PNG, PDF.

Inspect file type, page/image dimensions, orientation, page count, DPI metadata where available, and source errors.

### AI planning

AI translates language + source metadata into `PrintJobSpec`.

The LLM does not calculate final physical coordinates and does not talk directly to the printer.

### Deterministic validation

Validate paper bounds, sizes, margins, copies, source readability, layout overflow, printer capabilities, and ambiguous requests.

### Layout

Support single-page fit, exact-size item, repeated items, grids, auto rotation, spacing, cut marks, and paper-saving placement.

### Duplex printing

`PrintJobSpec` expresses two-sided intent independently from printer hardware.

- printers that advertise automatic duplex may execute through the driver,
- simplex printers use a guided manual-duplex workflow,
- the user should not need to manually select odd/even pages or calculate reverse order,
- duplex intent must never be silently ignored and printed one-sided.

Manual duplex uses two deterministic print passes with one explicit paper-reinsert step. Printer-specific feed/order/rotation behavior must be physically calibrated before it is described as verified.

See `docs/MANUAL_DUPLEX.md`.

### Preview and policy

Safety modes:

- Safe: always preview.
- Smart: known low-risk jobs may print directly; new/ambiguous jobs preview.
- Auto: direct print only after explicit opt-in.

Default: Smart with conservative rules.

### Recipes

Validated settings can be saved as structured recipes such as ID photo 3x4, 4x6, CCCD, 10x15 photo, labels, document draft, or photo-quality A4.

### Scan

Scanner acquisition joins the normal pipeline after capture:

scanner -> acquire -> crop/deskew -> optional enhancement/OCR -> image/PDF -> PrintJobSpec pipeline.

## Non-functional requirements

- local-first printer/file control
- deterministic physical sizing
- recoverable errors
- printer/spooler status visible
- provider-neutral AI boundary
- layout engine works without AI once a spec exists
- repository documentation persists project state across sessions

## V1 success

JPG/PNG/PDF + natural-language request can produce a validated A4 preview and a correct physical print on Epson L3310 without navigating Epson's UI.

Exact-size output must eventually be verified with a real ruler/calibration page.
