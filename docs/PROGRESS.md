# Progress / Session Handoff

Last updated: 2026-09-27

## Current milestone

**M1 - Deterministic A4 engine**

## Verified foundation

- Repository source-of-truth docs are in place.
- .NET 10 domain/layout/web skeleton is merged to `main`.
- GitHub Actions CI run #3 is green on both:
  - Ubuntu: restore -> test -> web build
  - Windows: restore -> test -> web build
- Baseline layout tests pass:
  - A4 40x60 mm without rotation = 16 items/page
  - A4 40x60 mm with rotation = 18 items/page
  - oversized paper is rejected
- Railway project `InAnTuDong` created.
- Railway service `printai-web` deploys from `eddyvn98/InAnTuDong` branch `main`.
- Railway deployment reached `SUCCESS`.
- ASP.NET Core runtime log confirms the app is listening on `0.0.0.0:8080`.
- Railway public service domain created: `https://printai-web-production.up.railway.app`.

## Current implementation

- `PrintAI.Domain`
  - `PrintJobSpec`
  - validation
  - canonical mm-based geometry contract
- `PrintAI.Layout`
  - deterministic grid/repeat placement
  - automatic 90-degree rotation when capacity improves
- `PrintAI.Web`
  - `/health`
  - `/api/demo-layout`
  - browser A4 geometry preview demo
- Dockerfile
- Windows + Linux CI

## Exact next engineering tasks

1. Add exact-size single-item layout mode.
2. Add fit/contain/cover geometry.
3. Add SkiaSharp and render actual image content into the A4 preview.
4. Add cut-mark rendering.
5. Add JPG/PNG metadata inspection.
6. Add PDF page metadata inspection.
7. Start Windows printer capability probe for Epson L3310.
8. Add a calibration page for physical-size verification.

## Web verification note

Railway reports the deployment healthy and runtime logs show the web server listening on port 8080. The public domain is provisioned. The current execution environment could not resolve the newly-created Railway hostname immediately, so direct external HTTP verification from this session was not available at the moment of creation. Treat Railway deployment/runtime status as verified; re-check the public URL in a later session if needed.

## Not started

- real Windows spooler submission
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

Never continue from chat memory alone. Read this file, `AGENTS.md`, and the relevant specs first. Update this file before ending a work session.
