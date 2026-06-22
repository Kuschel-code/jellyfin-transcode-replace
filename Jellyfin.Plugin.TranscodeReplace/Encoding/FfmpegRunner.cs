using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>
/// Runs ffmpeg with a pre-built argument list. The list is passed through
/// <c>ProcessStartInfo.ArgumentList</c> (no shell re-parsing), so file names with
/// spaces or quotes cannot inject extra arguments. <see cref="Quote"/> exists only
/// to render a human-readable command line for logs.
/// </summary>
public sealed class FfmpegRunner
{
    private readonly IProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegRunner"/> class.
    /// </summary>
    /// <param name="runner">Process runner.</param>
    public FfmpegRunner(IProcessRunner runner) => _runner = runner;

    /// <summary>Runs ffmpeg asynchronously with an injection-safe argument list.</summary>
    /// <param name="ffmpegPath">Path to ffmpeg.</param>
    /// <param name="arguments">Argument list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process result.</returns>
    public Task<ProcessResult> RunAsync(string ffmpegPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => _runner.RunAsync(ffmpegPath, arguments, cancellationToken);

    /// <summary>Renders an argument list as a single command-line string, for logging only.</summary>
    /// <param name="arguments">Argument list.</param>
    /// <returns>Quoted command line.</returns>
    public static string Quote(IEnumerable<string> arguments) => string.Join(' ', arguments.Select(QuoteOne));

    private static string QuoteOne(string arg)
    {
        if (arg.Length == 0)
        {
            return "\"\"";
        }

        var needsQuote = arg.Any(c => char.IsWhiteSpace(c) || c == '"');
        return needsQuote ? "\"" + arg.Replace("\"", "\\\"") + "\"" : arg;
    }
}
