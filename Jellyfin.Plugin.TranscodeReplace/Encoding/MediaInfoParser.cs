using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>
/// Parses <c>ffprobe -print_format json -show_format -show_streams</c> output into a
/// <see cref="MediaSummary"/>. Pure function so it is unit testable without ffprobe.
/// </summary>
public static class MediaInfoParser
{
    /// <summary>Parses ffprobe JSON.</summary>
    /// <param name="ffprobeJson">Raw ffprobe JSON output.</param>
    /// <param name="sourcePath">The probed file path.</param>
    /// <returns>A populated media summary.</returns>
    public static MediaSummary Parse(string ffprobeJson, string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            ext = "mkv";
        }

        string? videoCodec = null;
        var tenBit = false;
        string? transfer = null, primaries = null, space = null;
        var dolbyVision = false;
        var audioCount = 0;
        var subtitles = new List<string>();
        double? duration = null;
        int? videoBitrateKbps = null;

        using var doc = JsonDocument.Parse(ffprobeJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            var haveVideo = false;
            foreach (var stream in streams.EnumerateArray())
            {
                var type = GetString(stream, "codec_type");
                if (type == "video" && !haveVideo)
                {
                    haveVideo = true;
                    videoCodec = GetString(stream, "codec_name");

                    var pix = GetString(stream, "pix_fmt");
                    var profile = GetString(stream, "profile");
                    var bitsPerRaw = GetString(stream, "bits_per_raw_sample");
                    tenBit = Is10BitPixFmt(pix)
                             || bitsPerRaw == "10"
                             || (profile?.Contains("10", StringComparison.Ordinal) ?? false);

                    transfer = GetString(stream, "color_transfer");
                    primaries = GetString(stream, "color_primaries");
                    space = GetString(stream, "color_space");
                    videoBitrateKbps = ToKbps(GetString(stream, "bit_rate"));

                    var tag = GetString(stream, "codec_tag_string");
                    if (tag is "dvh1" or "dvhe" || videoCodec is "dvhe" or "dvh1")
                    {
                        dolbyVision = true;
                    }

                    if (stream.TryGetProperty("side_data_list", out var sideData) && sideData.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in sideData.EnumerateArray())
                        {
                            var sdType = GetString(entry, "side_data_type");
                            if (sdType is not null &&
                                (sdType.Contains("DOVI", StringComparison.OrdinalIgnoreCase) ||
                                 sdType.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase)))
                            {
                                dolbyVision = true;
                            }
                        }
                    }
                }
                else if (type == "audio")
                {
                    audioCount++;
                }
                else if (type == "subtitle")
                {
                    var codec = GetString(stream, "codec_name");
                    if (codec is not null)
                    {
                        subtitles.Add(codec);
                    }
                }
            }
        }

        if (root.TryGetProperty("format", out var format))
        {
            duration = ToDouble(GetString(format, "duration"));
            videoBitrateKbps ??= ToKbps(GetString(format, "bit_rate"));
        }

        return new MediaSummary
        {
            SourcePath = sourcePath,
            Container = ext,
            VideoCodec = videoCodec,
            Is10Bit = tenBit,
            ColorTransfer = transfer,
            ColorPrimaries = primaries,
            ColorSpace = space,
            IsDolbyVision = dolbyVision,
            AudioStreamCount = audioCount,
            SubtitleCodecs = subtitles,
            DurationSeconds = duration,
            VideoBitrateKbps = videoBitrateKbps
        };
    }

    // Detects 10-bit from the pixel format without the false positive of a bare "10"
    // substring (e.g. yuv410p is 8-bit 4:1:0, not 10-bit).
    private static bool Is10BitPixFmt(string? pixFmt) =>
        !string.IsNullOrEmpty(pixFmt) &&
        (pixFmt.Contains("10le", StringComparison.Ordinal)
            || pixFmt.Contains("10be", StringComparison.Ordinal)
            || pixFmt.Contains("p010", StringComparison.Ordinal)
            || pixFmt.Contains("p10", StringComparison.Ordinal));

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? ToDouble(string? raw) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static int? ToKbps(string? rawBitsPerSecond) =>
        long.TryParse(rawBitsPerSecond, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bps) && bps > 0
            ? (int)(bps / 1000)
            : null;
}
