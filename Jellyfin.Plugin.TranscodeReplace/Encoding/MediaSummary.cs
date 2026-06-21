using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>
/// A probe summary of a source file used to build ffmpeg arguments and make
/// HDR / container decisions. In M3 a minimal summary is produced; M4 fills it
/// from a real ffprobe call.
/// </summary>
public sealed class MediaSummary
{
    /// <summary>Gets the source path.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Gets the source container/extension (no dot).</summary>
    public string Container { get; init; } = "mkv";

    /// <summary>Gets the source video codec.</summary>
    public string? VideoCodec { get; init; }

    /// <summary>Gets a value indicating whether the source is 10-bit.</summary>
    public bool Is10Bit { get; init; }

    /// <summary>Gets the color transfer characteristic (e.g. smpte2084).</summary>
    public string? ColorTransfer { get; init; }

    /// <summary>Gets the color primaries (e.g. bt2020).</summary>
    public string? ColorPrimaries { get; init; }

    /// <summary>Gets the color space.</summary>
    public string? ColorSpace { get; init; }

    /// <summary>Gets a value indicating whether Dolby Vision side data is present.</summary>
    public bool IsDolbyVision { get; init; }

    /// <summary>Gets the number of audio streams.</summary>
    public int AudioStreamCount { get; init; }

    /// <summary>Gets the subtitle codecs present.</summary>
    public IReadOnlyList<string> SubtitleCodecs { get; init; } = Array.Empty<string>();

    /// <summary>Gets the duration in seconds.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>Gets the video bitrate in kbps.</summary>
    public int? VideoBitrateKbps { get; init; }

    /// <summary>Gets the HDR classification derived from color metadata.</summary>
    public HdrType Hdr
    {
        get
        {
            if (IsDolbyVision)
            {
                return HdrType.DolbyVision;
            }

            return ColorTransfer?.ToLowerInvariant() switch
            {
                "smpte2084" => HdrType.Hdr10,
                "arib-std-b67" => HdrType.Hlg,
                _ => HdrType.None
            };
        }
    }

    /// <summary>Gets a value indicating whether the source has image-based subtitles (PGS/VobSub).</summary>
    public bool HasImageSubtitles => SubtitleCodecs.Any(c =>
        c?.ToLowerInvariant() is "hdmv_pgs_subtitle" or "pgssub" or "pgs"
            or "dvd_subtitle" or "vobsub" or "dvb_subtitle");

    /// <summary>
    /// Builds a minimal summary from just the file path (M3 placeholder).
    /// </summary>
    /// <param name="path">Source path.</param>
    /// <returns>A minimal summary.</returns>
    public static MediaSummary Minimal(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            ext = "mkv";
        }

        return new MediaSummary
        {
            SourcePath = path,
            Container = ext,
            AudioStreamCount = 1
        };
    }
}
