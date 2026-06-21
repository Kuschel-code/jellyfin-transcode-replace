using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.TranscodeReplace.Hardware;

/// <summary>
/// Real <see cref="IProcessRunner"/> backed by <see cref="Process"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public ProcessResult Run(string fileName, string arguments, int timeoutMs = 30000)
    {
        using var process = Create(fileName, arguments);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMs))
        {
            TryKill(process);
            return new ProcessResult(-1, stdout.ToString(), stderr.ToString() + "\n[timed out]");
        }

        // Ensure async output handlers have flushed.
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        using var process = Create(fileName, arguments);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static Process Create(string fileName, string arguments) => new()
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        },
        EnableRaisingEvents = true
    };

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
        catch (Exception)
        {
            // Best effort.
        }
    }
}
