using System.Diagnostics;

namespace PrintAI.Planning;

public sealed class AntigravityProcessRunner : IAntigravityCommandRunner
{
    public async Task<AntigravityCommandResult> RunAsync(
        AntigravityInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var start = new ProcessStartInfo
        {
            FileName = invocation.CliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Add(start, "-p", invocation.Prompt);
        Add(start, "--model", invocation.Model);
        Add(start, "--effort", invocation.Effort);
        Add(start, "--output-format", "json");
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

        var stdoutTask = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
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
