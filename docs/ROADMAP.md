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
- [ ] golden preview tests

Exit: JSON PrintJobSpec creates a trustworthy A4 preview.

## M2 - Windows print path

- [ ] enumerate installed printers
- [ ] select printer/profile
- [ ] query capabilities/printable area
- [ ] submit through Windows spooler/driver
- [ ] status/errors
- [ ] Epson L3310 calibration page
- [ ] physical measurement verification

## M3 - AI planner + desktop UX

- [ ] WPF/WebView2 shell
- [ ] files/folder input
- [ ] source inspection
- [ ] provider-neutral AI planner
- [ ] structured output parsing
- [ ] Safe/Smart/Auto policy
- [ ] preview approval
- [ ] job history

## M4 - Scan + recipes

- [ ] WIA adapter
- [ ] scan image/PDF
- [ ] crop/deskew
- [ ] reusable recipes
- [ ] confidence/direct-print rules

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
