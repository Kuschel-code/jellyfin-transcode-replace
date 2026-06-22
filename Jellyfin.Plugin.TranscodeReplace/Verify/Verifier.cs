using System;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;

namespace Jellyfin.Plugin.TranscodeReplace.Verify;

/// <summary>Outcome of verifying a transcoded output before replacing the original.</summary>
public enum VerifyOutcome
{
    /// <summary>All gates passed; safe to replace.</summary>
    Passed,

    /// <summary>A gate failed; the output is bad and must be discarded.</summary>
    Failed,

    /// <summary>The output is valid but not smaller than the source; do not replace.</summary>
    SkipNotSmaller
}

/// <summary>The verification result.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Reason">Human-readable reason.</param>
public sealed record VerificationResult(VerifyOutcome Outcome, string? Reason);

/// <summary>
/// Pre-replace verification gates (architecture plan section 3.7). Pure logic; the
/// caller is responsible for the ffmpeg exit code and the VMAF gate. Every gate must
/// pass before the original is touched.
/// </summary>
public static class Verifier
{
    private const double MaxDurationDrift = 0.005; // 0.5 %
    private const double MinSizeRatio = 0.02; // < 2 % of source is suspicious

    /// <summary>Runs the verification gates.</summary>
    /// <param name="source">Source summary.</param>
    /// <param name="output">Output summary (null if its ffprobe failed).</param>
    /// <param name="sourceSize">Source size in bytes.</param>
    /// <param name="outputSize">Output size in bytes.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>The verification result.</returns>
    public static VerificationResult Check(
        MediaSummary source,
        MediaSummary? output,
        long sourceSize,
        long outputSize,
        PluginConfiguration config)
    {
        if (output is null)
        {
            return Fail("output could not be probed");
        }

        if (outputSize <= 0)
        {
            return Fail("output is empty");
        }

        if (!CodecNames.Matches(output.VideoCodec, config.TargetVideoCodec))
        {
            return Fail($"output codec '{output.VideoCodec}' is not {config.TargetVideoCodec}");
        }

        if (output.DurationSeconds is not > 0)
        {
            return Fail("output has no valid duration");
        }

        if (source.DurationSeconds is > 0)
        {
            var drift = Math.Abs(output.DurationSeconds.Value - source.DurationSeconds.Value) / source.DurationSeconds.Value;
            if (drift > MaxDurationDrift)
            {
                return Fail($"duration drift {drift:P2} exceeds {MaxDurationDrift:P1}");
            }
        }

        if (output.AudioStreamCount < source.AudioStreamCount)
        {
            return Fail($"audio streams lost ({output.AudioStreamCount} < {source.AudioStreamCount})");
        }

        if (sourceSize > 0 && outputSize < sourceSize * MinSizeRatio)
        {
            return Fail("output is suspiciously small (< 2% of source)");
        }

        if (sourceSize > 0 && outputSize >= sourceSize)
        {
            return new VerificationResult(VerifyOutcome.SkipNotSmaller, "output is not smaller than the source");
        }

        return new VerificationResult(VerifyOutcome.Passed, null);
    }

    private static VerificationResult Fail(string reason) => new(VerifyOutcome.Failed, reason);
}
