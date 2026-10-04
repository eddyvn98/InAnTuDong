using System.Diagnostics;
using System.Text.Json;

namespace PrintAI.Planning;

public sealed class AntigravityProcessRunner : IAntigravityStreamingCommandRunner
{
    public async Task<AntigravityCommandResult> RunAsync(
        AntigravityInvocation invocation,
        CancellationToken cancellationToken = default) =>
        await RunCoreAsync(invocation, null, cancellationToken);

    public async Task<AntigravityCommandResult> RunStreamingAsync(
        AntigravityInvocation invocation,
        IProgress<PlannerProgressUpdate> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return await RunCoreAsync(invocation, progress, cancellationToken);
    }

    private static async Task<AntigravityCommandResult> RunCoreAsync(
        AntigravityInvocation invocation,
        IProgress<PlannerProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var start = new ProcessStartInfo
        {
            FileName = invocation.CliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(invocation.WorkingDirectory)
                ? Environment.CurrentDirectory
                : invocation.WorkingDirectory
        };

        Add(start, "-p", invocation.Prompt);
        Add(start, "--model", invocation.Model);
        if (!string.IsNullOrWhiteSpace(invocation.Effort))
            Add(start, "--effort", invocation.Effort);
        Add(start, "--output-format", progress is null ? "json" : "stream-json");
        if (!string.IsNullOrWhiteSpace(invocation.JsonSchema))
            Add(start, "--json-schema", invocation.JsonSchema);
        Add(start, "--print-timeout", invocation.PrintTimeout);
        start.ArgumentList.Add("--sandbox");

        using var process = new Process { StartInfo = start };

        try
        {
            if (!process.Start())
                throw new PlannerTransportException(
                    "Could not start Antigravity CLI.");
        }
        catch (Exception ex) when (
            ex is System.ComponentModel.Win32Exception or
            InvalidOperationException)
        {
            throw new PlannerTransportException(
                $"Could not start Antigravity CLI at '{invocation.CliPath}'.",
                responseBody: null,
                ex);
        }

        var stdoutTask = progress is null
            ? process.StandardOutput.ReadToEndAsync(cancellationToken)
            : ReadStreamEventsAsync(process, progress, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static async Task<string> ReadStreamEventsAsync(
        Process process,
        IProgress<PlannerProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        string? status = null;
        string? response = null;
        string? error = null;
        JsonElement? structuredOutput = null;
        var output = new System.Text.StringBuilder();
        var timer = Stopwatch.StartNew();
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            output.AppendLine(line);
            try
            {
                using var json = JsonDocument.Parse(line);
                var root = json.RootElement;
                var eventName = root.TryGetProperty("event", out var name)
                    ? name.GetString()
                    : null;
                if (eventName == "init")
                {
                    var model = root.TryGetProperty("init", out var init) &&
                                init.TryGetProperty("model", out var modelNode)
                        ? modelNode.GetString()
                        : null;
                    progress.Report(new("status", model is null
                        ? "AGY đã bắt đầu xử lý yêu cầu."
                        : $"AGY đang suy luận với {model}.", ElapsedMilliseconds: timer.ElapsedMilliseconds));
                }
                else if (eventName == "step_update" && root.TryGetProperty("step_update", out var step))
                {
                    var delta = step.TryGetProperty("text_delta", out var text)
                        ? text.GetString()
                        : null;
                    var state = step.TryGetProperty("state", out var stateNode)
                        ? stateNode.GetString()
                        : null;
                    if (!string.IsNullOrEmpty(delta))
                        progress.Report(new("delta", "AGY đang tạo kết quả kế hoạch.", delta,
                            timer.ElapsedMilliseconds));
                    else if (state is not null)
                        progress.Report(new("status", $"AGY: {state.ToLowerInvariant()}.",
                            ElapsedMilliseconds: timer.ElapsedMilliseconds));
                }
                else if (eventName == "result" && root.TryGetProperty("result", out var result))
                {
                    status = result.TryGetProperty("status", out var statusNode)
                        ? statusNode.GetString()
                        : null;
                    response = result.TryGetProperty("response", out var responseNode)
                        ? responseNode.GetString()
                        : null;
                    structuredOutput = result.TryGetProperty("structured_output", out var structuredNode) &&
                                       structuredNode.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                        ? structuredNode.Clone()
                        : null;
                    error = result.TryGetProperty("error", out var errorNode)
                        ? errorNode.GetString()
                        : null;
                    progress.Report(new("status", "AGY đã hoàn tất phản hồi.",
                        ElapsedMilliseconds: timer.ElapsedMilliseconds));
                }
            }
            catch (JsonException)
            {
                // Keep raw output for diagnostics if the CLI emits a non-event line.
            }
        }

        if (status is null)
            return output.ToString();
        return structuredOutput is { } structured
            ? JsonSerializer.Serialize(new { status, structured_output = structured, response, error })
            : JsonSerializer.Serialize(new { status, response, error });
    }

    private static void Add(
        ProcessStartInfo start,
        string name,
        string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort only. Cancellation should still flow to the caller.
        }
    }
}
