# Print AI - Windows package

## Requirements

- Windows 10/11 x64
- Microsoft Edge WebView2 Runtime
- installed Windows printer driver for the target printer
- Epson L3310 driver when using the verified L3310 profile
- LibreOffice only when importing Word/Excel/PowerPoint files

The package is self-contained for .NET. You do not need the .NET SDK to run it.

## Verify the download

The GitHub Actions artifact includes:

- `PrintAI-win-x64.zip`
- `PrintAI-win-x64.sha256.txt`

Before extracting, verify the ZIP:

```powershell
$actual = (Get-FileHash .\PrintAI-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
$expected = ((Get-Content .\PrintAI-win-x64.sha256.txt) -split "\s+")[0].ToLowerInvariant()
if ($actual -ne $expected) { throw "PrintAI package checksum mismatch." }
"Checksum OK: $actual"
```

`BUILD.txt` inside the ZIP identifies the product version, runtime, build configuration and source commit.

## Install

After verifying the ZIP checksum, extract the whole package to a temporary/normal folder.

Recommended per-user install:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-PrintAI.ps1
```

Default install location:

```text
%LOCALAPPDATA%\Programs\PrintAI
```

The installer does not require administrator rights. It creates Start Menu and Desktop shortcuts and launches Print AI after installation.

### Upgrade

Run `Install-PrintAI.ps1` from the newly extracted release package again.

The new package is staged and verified before it replaces the current install. The old install is retained as:

```text
%LOCALAPPDATA%\Programs\PrintAI.previous
```

### Rollback

From an extracted Print AI package, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Rollback-PrintAI.ps1
```

Rollback swaps the current install with the saved previous install, so the version you rolled back from remains available as the next rollback target.

### Uninstall

From an extracted Print AI package, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PrintAI.ps1
```

Uninstall removes application files and shortcuts but keeps local history, recipes and work files by default.

To also remove Print AI local data:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PrintAI.ps1 -RemoveData
```

## Portable start

You may still use Print AI without installing it:

1. Extract the whole ZIP to a normal folder.
2. Keep all DLL/native files and the `ui` folder beside `PrintAI.exe`.
3. Run `PrintAI.exe`.
4. Select or drag a JPG, PNG, HEIC, PDF or supported Office document into the app.
5. Review the A4 preview before printing.

Do not move only `PrintAI.exe` out of the extracted folder. WebView2, PDFium and other native/runtime files are shipped beside it.

## AI planner

AI is optional. Manual preview and printing work without an AI endpoint.

You can configure the planner in the app with:

- chat-completions-compatible endpoint
- model name
- optional API key

The API key entered in the UI is kept only for the current process.

Managed/local configuration can instead use:

```text
PRINTAI_AI_ENDPOINT=https://your-endpoint/chat/completions
PRINTAI_AI_MODEL=your-model
PRINTAI_AI_API_KEY=optional-key
```

## Epson L3310

The repository's L3310 device profile has been physically calibrated and exact-size output through the PrintAI spooler path was measured successfully.

If the printer driver, borderless setting, media mode or scaling configuration changes, rerun the calibration before relying on exact physical dimensions.


## Office documents

DOC, DOCX, XLS, XLSX, PPT and PPTX are converted to PDF before they enter the PrintAI layout/preview pipeline.

Install LibreOffice normally, or set an explicit converter path:

```text
PRINTAI_LIBREOFFICE_PATH=C:\Program Files\LibreOffice\program\soffice.exe
```

PrintAI also checks the normal LibreOffice installation location and the system PATH.

LibreOffice is not bundled into the PrintAI ZIP. Image/PDF/HEIC printing continues to work without it.
