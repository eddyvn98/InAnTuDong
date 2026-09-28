# Product Specification

## Product

Print AI is a Windows-first, print-only assistant that accepts local files/folders plus a natural-language print request and converts them into accurate, reviewable print jobs without requiring the user to operate the printer manufacturer's app.

The product does not expand into CRM, quotation/pricing, payment, inventory, delivery, or general print-shop order management. Its responsibility ends at understanding, preparing, validating, previewing and executing printing.

## Primary interaction

1. User provides one or many file paths, a folder, drag/drop files, or scanner input.
2. User describes the outcome in natural language.
3. AI converts simple uniform intent to `PrintJobSpec 1.0`, or complex page/source-specific intent to `PrintPlan 2.0`.
4. A validated `PrintPlan 2.0` is deterministically compiled into one or more `PrintJobSpec 1.0` batches.
5. Deterministic code validates sizes/capabilities and calculates layout.
6. The system previews uncertain/risky jobs.
7. A Windows agent submits the job through the installed driver/spooler.

Examples:

- "In file này A4 1 bản."
- "Mỗi ảnh 4x6 cm, 2 bản, xếp tiết kiệm giấy, có đường cắt."
- "Ghép 2 mặt CCCD thành 3 bộ trên A4."
- "Xếp 3 ảnh này lên giấy 4x6 inch, cho tôi vài cách để chọn."
- "Scan 5 tờ thành một PDF rồi in 2 bản."
- "In file này dạng booklet A4 gấp thành A5, tự dàn trang và thêm trang trắng nếu cần."

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
- 4x6-inch photo paper when the installed driver advertises a matching paper size
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

Support single-page fit, exact-size item, repeated items, grids, auto rotation, spacing, cut marks, paper-saving placement, deterministic N-up, physical scaling, crop/anchor/offset/asymmetric margins, and booklet imposition.

For small-photo workflows, support deterministic auto-layout alternatives. The first slice accepts 1-6 selected source pages, generates up to four 4x6-inch (101.6 x 152.4 mm) grid candidates across portrait/landscape, renders each candidate for review, and lets the user choose one before printing. The LLM does not calculate auto-grid candidate coordinates.

Smart Collage / Layout V2 extends this with an explicit canvas scene graph: each frame can have independent geometry, z-order, mask shape, rotation, fit, image scale and image offset. The first template library targets three-photo 4x6 portrait collages with at least ten deterministic designs. AI photo analysis and template/transform selection are layered on top of this validated renderer rather than replacing it.

### Duplex printing

`PrintJobSpec` expresses two-sided intent independently from printer hardware.

- printers that advertise automatic duplex may execute through the driver
- simplex printers use a guided manual-duplex workflow
- the user does not manually calculate odd/even pages or reverse order
- duplex intent must never be silently ignored and printed one-sided
- pending back-pass state survives restart and retry
- printer-specific feed/order/rotation behavior is stored separately from ordinary scale/offset calibration
- manual-duplex verification requires explicit real-paper confirmation

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


## General print intent

The planner must eventually cover real print-language combinations without requiring a workflow preset for every product name. Required intent dimensions include page/range selection, ordered multi-file composition, copies vs complete sets/collation, mixed color rules, mixed simplex/duplex rules, N-up/repeat, scaling, crop/position, booklet imposition, tiled/poster printing, and variable-size items.

PrintPlan 2.0 now implements page selection, output groups, ordering, sets/collation, mixed color/duplex decomposition, General N-up, physical scaling, crop/placement, and booklet imposition before compiling to existing executable jobs. Poster/tiled printing and variable-size general composition remain the two corpus-driven M7 gaps. See `docs/GENERAL_PRINT_INTENT.md`.
