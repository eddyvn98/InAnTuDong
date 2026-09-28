using System.Text.Json;
using PrintAI.Domain;

namespace PrintAI.Desktop;

internal static class LocalWebActionDispatcher
{
    public static async Task ExecuteAsync(
        DesktopSession desktop,
        LocalWebAction request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(request);

        var payload = request.Payload;

        switch (request.Action)
        {
            case "configureAntigravity":
                desktop.ConfigureAntigravity(
                    ReadOptionalString(payload, "cliPath"));
                break;

            case "plan":
                await desktop.PlanAsync(
                    ReadString(payload, "request"),
                    ReadString(payload, "mode"));
                break;

            case "upsertQueuedRequest":
                desktop.UpsertQueuedRequest(
                    ReadOptionalString(payload, "id"),
                    ReadString(payload, "request"),
                    ReadString(payload, "mode"),
                    ReadIntArray(payload, "sourceIndexes"));
                break;

            case "removeQueuedRequest":
                desktop.RemoveQueuedRequest(
                    ReadString(payload, "id"));
                break;

            case "processQueuedRequest":
                await desktop.ProcessQueuedRequestAsync(
                    ReadString(payload, "id"));
                break;

            case "processNextQueuedRequest":
                await desktop.ProcessNextQueuedRequestAsync();
                break;

            case "selectPrinter":
                desktop.SelectPrinter(
                    ReadString(payload, "printer"));
                break;

            case "selectSource":
                desktop.SelectSource(
                    ReadInt(payload, "index"));
                break;

            case "removeSource":
                desktop.RemoveSource(
                    ReadInt(payload, "index"));
                break;

            case "selectPage":
                desktop.SelectPage(
                    ReadInt(payload, "index"));
                break;

            case "selectOutputPage":
                desktop.SelectOutputPage(
                    ReadInt(payload, "index"));
                break;

            case "selectPlanBatch":
                desktop.SelectPlanBatch(
                    ReadInt(payload, "index"));
                break;

            case "applyJobSettings":
                desktop.ApplyJobEdits(
                    ReadEdits(payload));
                break;

            case "print":
                desktop.PrintCurrent();
                break;

            case "printJob":
                desktop.PrintJob();
                break;

            case "printPlan":
                desktop.PrintPlan();
                break;

            case "printAllSources":
                desktop.PrintAllSources();
                break;

            case "continueManualDuplex":
                desktop.ContinueManualDuplex();
                break;

            case "cancelManualDuplex":
                desktop.CancelManualDuplex();
                break;

            case "refreshReadiness":
                desktop.RefreshReadiness();
                break;

            case "clearHistory":
                desktop.ClearHistory();
                break;

            case "clear":
                desktop.Clear();
                break;

            default:
                throw new NotSupportedException(
                    $"Local web action chưa hỗ trợ: {request.Action}");
        }
    }

    private static DesktopJobEdits ReadEdits(
        JsonElement payload)
    {
        if (!Enum.TryParse<LayoutMode>(
                ReadString(payload, "mode"),
                ignoreCase: true,
                out var mode))
        {
            throw new ArgumentException("Layout mode không hợp lệ.");
        }

        if (!Enum.TryParse<FitMode>(
                ReadString(payload, "fit"),
                ignoreCase: true,
                out var fit))
        {
            throw new ArgumentException("Fit mode không hợp lệ.");
        }

        if (!Enum.TryParse<DuplexMode>(
                ReadString(payload, "duplex"),
                ignoreCase: true,
                out var duplex))
        {
            throw new ArgumentException("Duplex mode không hợp lệ.");
        }

        return new(
            mode,
            ReadDouble(payload, "itemWidthMm"),
            ReadDouble(payload, "itemHeightMm"),
            ReadDouble(payload, "gapMm"),
            ReadDouble(payload, "marginMm"),
            ReadInt(payload, "copies"),
            duplex,
            ReadBool(payload, "allowRotate"),
            ReadBool(payload, "cutMarks"),
            fit);
    }

    private static string ReadString(
        JsonElement payload,
        string name) =>
        payload.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException(
                $"Thiếu giá trị {name}.");

    private static string? ReadOptionalString(
        JsonElement payload,
        string name) =>
        payload.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int ReadInt(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name).GetInt32();

    private static IReadOnlyList<int> ReadIntArray(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name)
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();

    private static double ReadDouble(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name).GetDouble();

    private static bool ReadBool(
        JsonElement payload,
        string name) =>
        payload.GetProperty(name).GetBoolean();
}
