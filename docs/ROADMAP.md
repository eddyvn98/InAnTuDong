# Roadmap

## M0 - Foundation

- [x] product specification
- [x] architecture
- [x] PrintJobSpec boundary
- [x] dependency policy
- [x] session handoff rules
- [x] persistent progress file
- [x] buildable domain/layout/web skeleton
- [x] GitHub Actions definition for Windows + Linux
- [x] CI observed green on GitHub
- [x] Railway deployment observed healthy

Exit: repo itself is enough to resume the project.

## M1 - Deterministic A4 engine

- [x] domain model
- [x] baseline validator
- [x] grid/repeat layout
- [x] choose 90-degree rotation when it fits more items
- [x] exact-size single item mode
- [x] fit/contain/cover rules
- [x] cut marks
- [x] SkiaSharp preview renderer
- [x] JPG/PNG metadata inspector
- [x] PDF metadata/page inspector
- [x] basic mm geometry tests
- [x] golden preview tests

Exit: JSON PrintJobSpec creates a trustworthy A4 preview.

## M2 - Windows print path

- [x] enumerate installed printers
- [x] select printer/profile
- [x] query capabilities/printable area
- [x] submit through Windows spooler/driver
- [x] status/errors
- [x] Epson L3310 calibration page
- [x] physical measurement verification
- [x] exact-size spooler physical verification

## M3 - AI planner + desktop UX

- [x] WPF/WebView2 shell
- [x] files/folder input
- [x] source inspection
- [x] PDF rasterization
- [x] multi-source/multi-page preview
- [x] provider-neutral AI planner
- [x] structured output parsing
- [x] Safe/Smart/Auto policy
- [x] preview approval
- [x] desktop natural-language planner integration
- [x] deterministic user-editable job settings
- [x] multi-output A4 preview/print
- [x] job history
- [x] desktop package/publish

## M4 - Scan + recipes

- [x] WIA adapter implementation
- [x] scan image/PDF pipeline
- [x] crop/deskew processing
- [x] reusable recipes
- [x] confidence/direct-print recipe rules
- [ ] physical Epson L3310 scan validation

## M5 - Advanced workflows

- [ ] CCCD
- [ ] ID photo presets
- [ ] labels/stickers
- [ ] content-to-layout
- [ ] mixed jobs
- [ ] HEIC
- [ ] Office conversion
- [ ] additional printer profiles

## Priority

Do not invest heavily in AI prompting before physical sizing and the Windows print path are reliable.
