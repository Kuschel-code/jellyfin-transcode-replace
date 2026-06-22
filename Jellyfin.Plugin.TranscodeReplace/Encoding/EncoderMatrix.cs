using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>The encoder-specific quality args and pixel format.</summary>
/// <param name="PixelFormat">Chosen pixel format.</param>
/// <param name="QualityArgs">Quality-control ffmpeg args.</param>
public sealed record EncoderPlan(string PixelFormat, IReadOnlyList<string> QualityArgs);

/// <summary>
/// Maps an encoder + quality value to its ffmpeg quality args and pixel format
/// (architecture plan section 3.2). Pure logic.
/// </summary>
public static class EncoderMatrix
{
    /// <summary>Builds the encoder plan.</summary>
    /// <param name="encoder">Chosen encoder.</param>
    /// <param name="quality">CRF/CQ/QP quality value.</param>
    /// <param name="tenBit">Whether to encode 10-bit.</param>
    /// <param name="softwarePreset">Software preset (x264/x265) / mapped for SVT-AV1.</param>
    /// <returns>The encoder plan.</returns>
    public static EncoderPlan Build(EncoderCap encoder, int quality, bool tenBit, string softwarePreset)
    {
        var q = quality.ToString(CultureInfo.InvariantCulture);

        var pix = encoder.Kind == HwKind.Software
            ? (tenBit ? "yuv420p10le" : "yuv420p")
            : (tenBit ? "p010le" : "nv12");

        IReadOnlyList<string> qualityArgs = encoder.Name switch
        {
            "libx264" or "libx265" => new[] { "-crf", q, "-preset", softwarePreset },
            "libsvtav1" => new[] { "-crf", q, "-preset", SvtPreset(softwarePreset) },
            // libvpx-vp9 constant-quality mode needs -b:v 0 alongside -crf; row-mt
            // enables tile threading and cpu-used trades speed for quality.
            "libvpx-vp9" => new[] { "-crf", q, "-b:v", "0", "-deadline", "good", "-cpu-used", Vp9CpuUsed(softwarePreset), "-row-mt", "1" },
            _ => encoder.Kind switch
            {
                HwKind.Nvenc => new[] { "-preset", "p5", "-cq", q },
                HwKind.Qsv => new[] { "-global_quality", q },
                HwKind.Vaapi => new[] { "-qp", q },
                HwKind.Amf => new[] { "-qp_i", q, "-qp_p", q },
                HwKind.VideoToolbox => new[] { "-q:v", q },
                _ => new[] { "-crf", q }
            }
        };

        return new EncoderPlan(pix, qualityArgs);
    }

    private static string SvtPreset(string softwarePreset) => softwarePreset switch
    {
        "veryslow" => "2",
        "slower" => "3",
        "slow" => "4",
        "medium" => "6",
        "fast" => "8",
        "faster" => "9",
        "veryfast" => "10",
        _ => "6"
    };

    // libvpx-vp9 cpu-used: 0 is slowest/best, higher is faster (0-5 in good mode).
    private static string Vp9CpuUsed(string softwarePreset) => softwarePreset switch
    {
        "veryslow" => "0",
        "slower" => "1",
        "slow" => "1",
        "medium" => "2",
        "fast" => "3",
        "faster" => "4",
        "veryfast" => "5",
        _ => "2"
    };
}
