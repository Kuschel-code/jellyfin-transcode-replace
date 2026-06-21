using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.TranscodeReplace.Hardware;

/// <summary>Outcome of running a child process.</summary>
/// <param name="ExitCode">Process exit code (-1 on timeout).</param>
/// <param name="StandardOutput">Captured stdout.</param>
/// <param name="StandardError">Captured stderr.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Gets a value indicating whether the process exited cleanly.</summary>
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Abstraction over child-process execution so probing and ffmpeg invocation
/// can be unit tested with a fake runner.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Runs a process synchronously and captures its output.</summary>
    /// <param name="fileName">Executable path.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>The process result.</returns>
    ProcessResult Run(string fileName, string arguments, int timeoutMs = 30000);

    /// <summary>Runs a process asynchronously and captures its output.</summary>
    /// <param name="fileName">Executable path.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task producing the process result.</returns>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
