using System.Collections.Generic;
using Jellyfin.Plugin.TranscodeReplace.Configuration;

namespace Jellyfin.Plugin.TranscodeReplace.Hardware;

/// <summary>A candidate encoder to probe.</summary>
/// <param name="Encoder">ffmpeg encoder name.</param>
/// <param name="Kind">Hardware backend kind.</param>
/// <param name="Codec">Target codec produced.</param>
/// <param name="ProbeHwOpts">Extra ffmpeg args required to probe this backend.</param>
public sealed record EncoderCandidate(string Encoder, HwKind Kind, TargetVideoCodec Codec, string ProbeHwOpts);

/// <summary>
/// The full matrix of encoders the probe will try, per the architecture plan
/// section 3.2.
/// </summary>
public static class EncoderCandidates
{
    /// <summary>Gets all encoder candidates in default priority order (HW before SW).</summary>
    public static readonly IReadOnlyList<EncoderCandidate> All = new List<EncoderCandidate>
    {
        // NVIDIA NVENC
        new("h264_nvenc", HwKind.Nvenc, TargetVideoCodec.H264, string.Empty),
        new("hevc_nvenc", HwKind.Nvenc, TargetVideoCodec.Hevc, string.Empty),
        new("av1_nvenc", HwKind.Nvenc, TargetVideoCodec.Av1, string.Empty),

        // Intel Quick Sync
        new("h264_qsv", HwKind.Qsv, TargetVideoCodec.H264, string.Empty),
        new("hevc_qsv", HwKind.Qsv, TargetVideoCodec.Hevc, string.Empty),
        new("av1_qsv", HwKind.Qsv, TargetVideoCodec.Av1, string.Empty),
        new("vp9_qsv", HwKind.Qsv, TargetVideoCodec.Vp9, string.Empty),

        // VA-API (Linux) - needs a render device plus an upload filter even to probe.
        new("h264_vaapi", HwKind.Vaapi, TargetVideoCodec.H264, "-vaapi_device /dev/dri/renderD128 -vf format=nv12,hwupload"),
        new("hevc_vaapi", HwKind.Vaapi, TargetVideoCodec.Hevc, "-vaapi_device /dev/dri/renderD128 -vf format=nv12,hwupload"),
        new("av1_vaapi", HwKind.Vaapi, TargetVideoCodec.Av1, "-vaapi_device /dev/dri/renderD128 -vf format=nv12,hwupload"),
        new("vp9_vaapi", HwKind.Vaapi, TargetVideoCodec.Vp9, "-vaapi_device /dev/dri/renderD128 -vf format=nv12,hwupload"),

        // AMD AMF (Windows)
        new("h264_amf", HwKind.Amf, TargetVideoCodec.H264, string.Empty),
        new("hevc_amf", HwKind.Amf, TargetVideoCodec.Hevc, string.Empty),
        new("av1_amf", HwKind.Amf, TargetVideoCodec.Av1, string.Empty),

        // Apple VideoToolbox (macOS)
        new("h264_videotoolbox", HwKind.VideoToolbox, TargetVideoCodec.H264, string.Empty),
        new("hevc_videotoolbox", HwKind.VideoToolbox, TargetVideoCodec.Hevc, string.Empty),

        // Software
        new("libx264", HwKind.Software, TargetVideoCodec.H264, string.Empty),
        new("libx265", HwKind.Software, TargetVideoCodec.Hevc, string.Empty),
        new("libsvtav1", HwKind.Software, TargetVideoCodec.Av1, string.Empty),
        new("libvpx-vp9", HwKind.Software, TargetVideoCodec.Vp9, string.Empty),
    };
}
