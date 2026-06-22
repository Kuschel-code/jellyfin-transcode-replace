using System;
using System.Collections.Generic;
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
        => RunSync(CreateWithArguments(fileName, arguments), timeoutMs);

    /// <inheritdoc />
    public ProcessResult Run(string fileName, IReadOnlyList<string> argumentList, int timeoutMs = 30000)
        => RunSync(CreateWithArgumentList(fileName, argumentList), timeoutMs);

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        => RunAsyncInternal(CreateWithArguments(fileName, arguments), cancellationToken);

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> argumentList, CancellationToken cancellationToken = default)
        => RunAsyncInternal(CreateWithArgumentList(fileName, argumentList), cancellationToken);

    private static ProcessResult RunSync(Process process, int timeoutMs)
    {
        using (process)
        {
            var (stdout, stderr) = Attach(process);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                TryKill(process);
                return new ProcessResult(-1, stdout.ToString(), stderr.ToString() + "\n[timed out]");
            }

            process.WaitForExit();
            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        }
    }

    private static async Task<ProcessResult> RunAsyncInternal(Process process, CancellationToken cancellationToken)
    {
        using (process)
        {
            var (stdout, stderr) = Attach(process);
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
    }

    private static (StringBuilder Out, StringBuilder Err) Attach(Process process)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };
        return (stdout, stderr);
    }

    private static Process CreateWithArguments(string fileName, string arguments)
    {
        var process = NewProcess(fileName);
        process.StartInfo.Arguments = arguments;
        return process;
    }

    private static Process CreateWithArgumentList(string fileName, IReadOnlyList<string> argumentList)
    {
        var process = NewProcess(fileName);
        foreach (var arg in argumentList)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        return process;
    }

    private static Process NewProcess(string fileName) => new()
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
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
