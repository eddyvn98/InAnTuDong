# M6 - Hardening and Release

## Goal

Turn the completed M5 feature set into a Windows build that is easier to diagnose, verify, install and validate on real machines.

M6 does not expand print-layout scope unless a release-blocking defect requires it.

## Workstreams

### 1. System readiness

The desktop app should expose whether the current machine can execute the core print path and which optional capabilities are missing.

Checks:

- PrintAI work directory is writable
- at least one Windows printer is available
- selected printer profile physical-verification state
- WIA scanner availability
- LibreOffice availability for Office conversion
- AI planner configuration

Missing storage or all printers is a release-readiness failure for printing. Scanner, LibreOffice, AI and unverified printer calibration are warnings because the app remains partially usable.

### 2. Package identity and integrity

Every Windows package must carry:

- product name
- application version
- target runtime
- source commit
- build configuration
- SHA-256 for the ZIP

The package version comes from the desktop project instead of being duplicated in CI configuration.

### 3. Regression fixtures

Current automated release-regression coverage exercises raster inspection/rendering, generated multi-page PDF inspection/rendering and mixed image + PDF-page composition.

Continue adding representative fixtures for:

- raster image
- multi-page PDF
- mixed-source composition
- HEIC/HEIF decode
- Office-to-PDF adapter behavior

Large proprietary/user files must not be committed as fixtures.

### 4. Packaged smoke test

The desktop executable supports `--self-test`, which bypasses normal UI startup, exercises packaged assets and the raster render pipeline, writes a JSON report and exits non-zero on failure.

The self-contained win-x64 publish output is exercised before upload:

- executable and UI/native assets present
- app process can start on the CI runner
- no immediate missing-runtime/native-DLL failure

Do not automate actual physical printing in CI.

### 5. Install / upgrade / rollback

The M6 package ships PowerShell lifecycle scripts for a per-user install under `%LOCALAPPDATA%\Programs\PrintAI`.

Upgrade stages the new release before moving the current install to `.previous`. Rollback swaps current and previous. Uninstall removes application files while keeping PrintAI local user data unless `-RemoveData` is explicitly requested.

The package workflow exercises this lifecycle in a sandbox before upload. The portable ZIP remains supported.

### 6. Support diagnostics

The desktop app can export a JSON support report from the System Readiness panel.

The report contains version/runtime, OS, non-content source counts, printer/profile state, scanner names, optional-capability availability and readiness checks.

It deliberately excludes credentials, AI endpoint configuration, user source paths, job history and document content.

### 7. Duplex intent correctness

Duplex is release-blocking because schema 1.0 already accepts two-sided intent.

Required before tagged preview release:

- `duplex=off` continues through the existing simplex path
- `duplex=longEdge|shortEdge` cannot use the one-page simplex submission path
- automatic-duplex printers use one multi-page driver batch
- simplex printers use the guided manual front/reinsert/back state machine
- physical sheets, copies and odd page counts are planned deterministically
- front and back passes are multi-page batches
- pending back-pass state survives restart
- back-pass failure retries backs without reprinting fronts
- printer/profile changes between passes are blocked
- per-printer reinsert order/rotation is explicit profile data
- verified manual-duplex status requires real-paper confirmation

Software implementation was merged in PR #29. CI and the Windows package workflow are green; physical printer calibration remains required. See `docs/MANUAL_DUPLEX.md`.

### 8. Field validation matrix

Track real-machine results separately from automated tests:

- Epson L3310 print + WIA scan
- representative iPhone HEIC/HEIF
- DOCX/XLSX/PPTX through installed LibreOffice
- any additional printer model before marking its profile physically verified

## M6 exit criteria

M6 can close when:

1. CI is green on Ubuntu and Windows.
2. self-contained Windows package is green and has a SHA-256 checksum.
3. target Windows machine shows no readiness failures for the intended print path.
4. packaged smoke test passes.
5. representative format regression coverage is present.
6. install/upgrade/rollback instructions are documented and exercised.
7. support diagnostics can be exported without exposing source content or credentials.
8. real-world validation results are recorded without overstating unverified hardware.
9. duplex intent cannot silently degrade to one-sided output; automated manual-duplex coverage is green and the target printer's physical behavior is recorded.
