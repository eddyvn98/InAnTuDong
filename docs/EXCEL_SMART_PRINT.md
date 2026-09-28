# Excel Smart Print

## Goal

Turn an unprepared XLSX workbook into a readable A4 print source without using "fit every column on one page" as the default.

The original workbook is never modified. PrintAI creates an optimized copy in its local work directory, converts that copy to PDF through LibreOffice, then reuses the normal preview/print pipeline.

## Flow

```text
XLSX source
  -> workbook inspection
  -> AI spreadsheet plan (when configured)
       OR deterministic heuristic fallback
  -> strict SpreadsheetPrintPlan validation
  -> deterministic XLSX optimizer
  -> LibreOffice PDF conversion
  -> PrintAI preview
  -> normal policy / Windows print path
```

## Readability guardrails

The AI cannot directly edit workbook XML.

The plan is limited to:

- portrait or landscape
- minimum font size: 8.5-14 pt
- maximum column width: 10-45 character units
- wrap text on/off
- repeat 0-3 header rows
- repeat 0-3 leading columns
- scale: 90-100%

The validator rejects any plan whose effective printed text would be below 8.5 pt.

Horizontal pagination is allowed and is preferred over shrinking all columns into one unreadable page.

## Default heuristic

For a wide sheet, PrintAI currently prefers:

- A4 landscape
- minimum font 9.5 pt
- scale 100%
- wrap text
- capped column width
- repeat the detected header row
- repeat one or two leading columns

The optimizer also writes the used data range as the print area.

## AI context

The spreadsheet AI planner receives workbook structure, not arbitrary filesystem access.

It sees:

- workbook file name
- sheet names
- row/column counts
- estimated widths
- merged/hidden-column counts
- detected header row
- column headers and maximum text lengths

The AI returns only the constrained SpreadsheetPrintPlan. The deterministic optimizer applies it.

## Current scope

Smart Print v1 supports XLSX.

Legacy XLS files continue through the normal Office conversion path.

Semantic column grouping into explicitly named page groups can be added later. The current version relies on natural horizontal pagination plus repeated leading identifier columns.
