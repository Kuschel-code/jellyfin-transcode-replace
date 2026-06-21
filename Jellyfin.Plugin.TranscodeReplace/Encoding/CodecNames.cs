using System;
using Jellyfin.Plugin.TranscodeReplace.Configuration;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>
/// Maps target codecs to ffprobe codec names and matches source streams.
/// </summary>
public static class CodecNames
{
    /// <summary>Gets the ffprobe/ffmpeg codec name for a target codec.</summary>
    /// <param name="codec">Target codec.</param>
    /// <returns>Codec name as reported by ffprobe.</returns>
    public static string FfprobeName(TargetVideoCodec codec) => codec switch
    {
        TargetVideoCodec.H264 => "h264",
        TargetVideoCodec.Hevc => "hevc",
        TargetVideoCodec.Av1 => "av1",
        _ => "hevc"
    };

    /// <summary>
    /// Whether a source video stream codec already matches the target codec.
    /// HEVC is sometimes reported as <c>h265</c>.
    /// </summary>
    /// <param name="streamCodec">Source stream codec.</param>
    /// <param name="target">Target codec.</param>
    /// <returns>True if they match.</returns>
    public static bool Matches(string? streamCodec, TargetVideoCodec target)
    {
        if (string.IsNullOrEmpty(streamCodec))
        {
            return false;
        }

        var name = FfprobeName(target);
        return streamCodec.Equals(name, StringComparison.OrdinalIgnoreCase)
               || (target == TargetVideoCodec.Hevc && streamCodec.Equals("h265", StringComparison.OrdinalIgnoreCase));
    }
}
