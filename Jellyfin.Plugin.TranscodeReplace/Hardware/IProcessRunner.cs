using System.Collections.Generic;
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
/// <remarks>
/// The argument-list overloads are preferred whenever any argument is derived
/// from a file path or other untrusted input: they are passed straight to
/// <c>ProcessStartInfo.ArgumentList</c>, so the OS does not re-parse a single
/// command string (no argument injection via crafted file names).
/// </remarks>
public interface IProcessRunner
{
    /// <summary>Runs a process synchronously with a single argument string.</summary>
    /// <param name="fileName">Executable path.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>The process result.</returns>
    ProcessResult Run(string fileName, string arguments, int timeoutMs = 30000);

    /// <summary>Runs a process synchronously with an argument list (injection-safe).</summary>
    /// <param name="fileName">Executable path.</param>
    /// <param name="argumentList">Arguments passed verbatim, one per element.</param>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>The process result.</returns>
    ProcessResult Run(string fileName, IReadOnlyList<string> argumentList, int timeoutMs = 30000);

    /// <summary>Runs a process asynchronously with a single argument string.</summary>
    /// <param name="fileName">Executable path.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task producing the process result.</returns>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);

    /// <summary>Runs a process asynchronously with an argument list (injection-safe).</summary>
    /// <param name="fileName">Executable path.</param>
    /// <param name="argumentList">Arguments passed verbatim, one per element.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task producing the process result.</returns>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> argumentList, CancellationToken cancellationToken = default);
}
