using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>
/// Probes a media file with ffprobe and returns a <see cref="MediaSummary"/>.
/// The path is passed as a separate argument (no shell parsing).
/// </summary>
public sealed class MediaInfoProbe
{
    private readonly IProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaInfoProbe"/> class.
    /// </summary>
    /// <param name="runner">Process runner.</param>
    public MediaInfoProbe(IProcessRunner runner) => _runner = runner;

    /// <summary>Probes a file. Returns null if ffprobe fails or output is unparseable.</summary>
    /// <param name="ffprobePath">Path to ffprobe.</param>
    /// <param name="filePath">File to probe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A media summary, or null on failure.</returns>
    public async Task<MediaSummary?> ProbeAsync(string ffprobePath, string filePath, CancellationToken cancellationToken)
    {
        var args = new[]
        {
            "-v", "quiet",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            filePath
        };

        var result = await _runner.RunAsync(ffprobePath, args, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        try
        {
            return MediaInfoParser.Parse(result.StandardOutput, filePath);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
