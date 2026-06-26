using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Verify;

/// <summary>Parses a VMAF score out of ffmpeg libvmaf output. Pure.</summary>
public static class VmafParser
{
    private static readonly Regex ScoreRegex =
        new(@"VMAF score:\s*([0-9]+(?:\.[0-9]+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Extracts the VMAF score.</summary>
    /// <param name="ffmpegOutput">Combined ffmpeg output.</param>
    /// <returns>The score, or null if not found.</returns>
    public static double? Parse(string ffmpegOutput)
    {
        var match = ScoreRegex.Match(ffmpegOutput);
        return match.Success &&
               double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var score)
            ? score
            : null;
    }
}

/// <summary>
/// Runs ffmpeg's libvmaf filter to score a distorted output against its source
/// (plan section 3.7 / 3.8). Both inputs must share resolution and frame rate, which
/// holds for a straight re-encode without scaling.
/// </summary>
public sealed class VmafGate
{
    private readonly IProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="VmafGate"/> class.
    /// </summary>
    /// <param name="runner">Process runner.</param>
    public VmafGate(IProcessRunner runner) => _runner = runner;

    /// <summary>Builds the ffmpeg argument list for a VMAF comparison.</summary>
    /// <param name="distortedPath">The transcoded output.</param>
    /// <param name="referencePath">The original source.</param>
    /// <param name="threads">libvmaf thread count.</param>
    /// <returns>The argument list.</returns>
    public static IReadOnlyList<string> BuildArgs(string distortedPath, string referencePath, int threads) => new[]
    {
        "-hide_banner",
        "-i", distortedPath,
        "-i", referencePath,
        "-lavfi", string.Format(CultureInfo.InvariantCulture, "libvmaf=n_threads={0}", threads),
        "-f", "null",
        "-"
    };

    /// <summary>Scores the output. Returns null if VMAF could not be computed.</summary>
    /// <param name="ffmpegPath">Path to ffmpeg.</param>
    /// <param name="distortedPath">The transcoded output.</param>
    /// <param name="referencePath">The original source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The VMAF score, or null.</returns>
    public async Task<double?> ScoreAsync(string ffmpegPath, string distortedPath, string referencePath, CancellationToken cancellationToken)
    {
        var threads = Math.Max(1, Environment.ProcessorCount / 2);
        var args = BuildArgs(distortedPath, referencePath, threads);
        var result = await _runner.RunAsync(ffmpegPath, args, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            // libvmaf missing, decode error or resolution mismatch: no trustworthy score.
            return null;
        }

        return VmafParser.Parse(result.StandardError + "\n" + result.StandardOutput);
    }
}
