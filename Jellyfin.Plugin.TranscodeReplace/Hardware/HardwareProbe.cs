using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.TranscodeReplace.Hardware;

/// <summary>
/// Detects usable video encoders by parsing <c>ffmpeg -encoders</c> and then
/// running a real 1-second probe-encode per candidate (section 3.1). Results
/// are cached because probing spawns processes.
/// </summary>
public sealed class HardwareProbe
{
    private readonly IProcessRunner _runner;
    private readonly object _lock = new();
    private IReadOnlyList<EncoderCap>? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="HardwareProbe"/> class.
    /// </summary>
    /// <param name="runner">Process runner abstraction.</param>
    public HardwareProbe(IProcessRunner runner) => _runner = runner;

    /// <summary>
    /// Probes all candidate encoders. Cached after the first call.
    /// </summary>
    /// <param name="ffmpegPath">Path to the ffmpeg binary.</param>
    /// <param name="forceRefresh">Force re-probing.</param>
    /// <returns>All candidates with present/usable flags.</returns>
    public IReadOnlyList<EncoderCap> Detect(string ffmpegPath, bool forceRefresh = false)
    {
        lock (_lock)
        {
            if (_cache is not null && !forceRefresh)
            {
                return _cache;
            }

            var available = ListEncoders(ffmpegPath);
            var caps = new List<EncoderCap>();
            foreach (var candidate in EncoderCandidates.All)
            {
                var present = available.Contains(candidate.Encoder);
                var usable = present && ProbeEncode(ffmpegPath, candidate.Encoder, candidate.ProbeHwOpts);
                caps.Add(new EncoderCap(candidate.Encoder, candidate.Kind, candidate.Codec, present, usable));
            }

            _cache = caps;
            return _cache;
        }
    }

    /// <summary>Gets only the usable encoders.</summary>
    /// <param name="ffmpegPath">Path to the ffmpeg binary.</param>
    /// <returns>Usable encoders.</returns>
    public IReadOnlyList<EncoderCap> Usable(string ffmpegPath) =>
        Detect(ffmpegPath).Where(c => c.Usable).ToList();

    /// <summary>Lists encoder names the ffmpeg binary advertises.</summary>
    /// <param name="ffmpegPath">Path to the ffmpeg binary.</param>
    /// <returns>Set of encoder names.</returns>
    internal HashSet<string> ListEncoders(string ffmpegPath)
    {
        var result = _runner.Run(ffmpegPath, "-hide_banner -encoders");
        return ParseEncoderNames(result.StandardOutput + "\n" + result.StandardError);
    }

    /// <summary>
    /// Parses the output of <c>ffmpeg -encoders</c>. Video encoder lines start
    /// with a 6-character capability block whose first character is <c>V</c>,
    /// followed by the encoder name. Pure function for unit testing.
    /// </summary>
    /// <param name="encodersOutput">Raw command output.</param>
    /// <returns>Set of video encoder names.</returns>
    internal static HashSet<string> ParseEncoderNames(string encodersOutput)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in encodersOutput.Split('\n'))
        {
            var trimmed = raw.TrimEnd('\r').TrimStart();
            if (trimmed.Length < 8 || trimmed[0] != 'V')
            {
                continue;
            }

            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts[0].Length != 6)
            {
                continue;
            }

            // Skip the legend line "V..... = Video".
            if (parts[1] == "=")
            {
                continue;
            }

            names.Add(parts[1]);
        }

        return names;
    }

    /// <summary>
    /// Runs a 1-second probe-encode. Exit code 0 means the encoder really works
    /// on this machine (device + driver present), not merely compiled in.
    /// </summary>
    /// <param name="ffmpegPath">Path to the ffmpeg binary.</param>
    /// <param name="encoder">Encoder name.</param>
    /// <param name="hwOpts">Backend-specific probe options.</param>
    /// <returns>True if the probe-encode succeeded.</returns>
    internal bool ProbeEncode(string ffmpegPath, string encoder, string hwOpts)
    {
        var hw = string.IsNullOrWhiteSpace(hwOpts) ? string.Empty : hwOpts + " ";
        var args = "-hide_banner -f lavfi -i testsrc=duration=1:size=256x256:rate=5 " +
                   hw +
                   $"-c:v {encoder} -f null -";
        var result = _runner.Run(ffmpegPath, args, 20000);
        return result.ExitCode == 0;
    }
}
