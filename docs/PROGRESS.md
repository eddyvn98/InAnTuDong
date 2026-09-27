# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

M0 verification -> M1 deterministic A4 engine.

## Completed in source

- product/architecture/spec/roadmap documentation
- persistent AI handoff rules in `AGENTS.md`
- .NET 10 domain project
- baseline `PrintJobSpec`
- validator
- deterministic grid/repeat layout
- automatic 90-degree rotation when it increases capacity
- xUnit tests for A4 geometry and validation
- ASP.NET Core web demo with `/health` and `/api/demo-layout`
- Dockerfile suitable for Railway
- GitHub Actions CI definition for Ubuntu + Windows

## Verification still required

- observe GitHub Actions CI reach green terminal status
- deploy web service to Railway
- verify Railway deployment reaches SUCCESS
- open public web/health endpoint and verify HTTP response

## Exact next engineering tasks after verification

1. add exact-size single-item layout
2. add fit/contain/cover calculations
3. introduce SkiaSharp preview rendering
4. inspect JPG/PNG metadata
5. inspect PDF page metadata
6. begin Windows printer capability probe

## Not started

- real spooler submission
- AI provider integration
- WebView2 desktop shell
- scanner control
- recipes

## Known risks

- driver printable area changes with media/quality/borderless mode
- exact-size output requires physical calibration on Epson L3310
- scanner may require TWAIN fallback if WIA is insufficient
- PDF rendering/import may need a separate library from PDF authoring/manipulation

## Session rule

Never continue from chat memory alone. Read this file plus the relevant specs, then update this file before ending a work session.
