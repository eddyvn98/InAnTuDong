# Print AI - Windows package

## Requirements

- Windows 10/11 x64
- Microsoft Edge WebView2 Runtime
- installed Windows printer driver for the target printer
- Epson L3310 driver when using the verified L3310 profile

The package is self-contained for .NET. You do not need the .NET SDK to run it.

## Start

1. Extract the whole ZIP to a normal folder.
2. Keep all DLL/native files and the `ui` folder beside `PrintAI.exe`.
3. Run `PrintAI.exe`.
4. Select or drag a JPG, PNG or PDF into the app.
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
