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

Add representative automated fixtures for:

- raster image
- multi-page PDF
- mixed-source composition
- HEIC/HEIF decode
- Office-to-PDF adapter behavior

Large proprietary/user files must not be committed as fixtures.

### 4. Packaged smoke test

Exercise the self-contained win-x64 publish output before upload:

- executable and UI/native assets present
- app process can start on the CI runner
- no immediate missing-runtime/native-DLL failure

Do not automate actual physical printing in CI.

### 5. Install / upgrade / rollback

Define a user-safe Windows installation path and document upgrade/rollback.

The existing portable ZIP remains supported until an installer is verified.

### 6. Field validation matrix

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
7. real-world validation results are recorded without overstating unverified hardware.
