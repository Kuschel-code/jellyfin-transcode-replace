using Jellyfin.Plugin.TranscodeReplace.Configuration;

namespace Jellyfin.Plugin.TranscodeReplace.Hardware;

/// <summary>
/// The result of probing a single encoder: whether the ffmpeg binary lists it
/// (<paramref name="Present"/>) and whether a real probe-encode succeeded
/// (<paramref name="Usable"/>).
/// </summary>
/// <param name="Name">ffmpeg encoder name, e.g. <c>hevc_nvenc</c>.</param>
/// <param name="Kind">Hardware backend kind.</param>
/// <param name="Codec">Target video codec the encoder produces.</param>
/// <param name="Present">Whether the encoder is compiled into the binary.</param>
/// <param name="Usable">Whether a 1-second probe-encode returned exit code 0.</param>
public sealed record EncoderCap(string Name, HwKind Kind, TargetVideoCodec Codec, bool Present, bool Usable);
