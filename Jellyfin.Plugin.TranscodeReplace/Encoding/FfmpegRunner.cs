using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>
/// Runs ffmpeg with a pre-built argument list. Quoting is exposed and pure so
/// it can be unit tested independently of process execution.
/// </summary>
public sealed class FfmpegRunner
{
    private readonly IProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegRunner"/> class.
    /// </summary>
    /// <param name="runner">Process runner.</param>
    public FfmpegRunner(IProcessRunner runner) => _runner = runner;

    /// <summary>Runs ffmpeg asynchronously.</summary>
    /// <param name="ffmpegPath">Path to ffmpeg.</param>
    /// <param name="arguments">Argument list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process result.</returns>
    public Task<ProcessResult> RunAsync(string ffmpegPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => _runner.RunAsync(ffmpegPath, Quote(arguments), cancellationToken);

    /// <summary>Quotes an argument list into a single command-line string.</summary>
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
