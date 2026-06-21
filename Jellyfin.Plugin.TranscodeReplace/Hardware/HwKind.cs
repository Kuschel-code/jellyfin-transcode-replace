namespace Jellyfin.Plugin.TranscodeReplace.Hardware;

/// <summary>Hardware backend kind for a video encoder.</summary>
public enum HwKind
{
    /// <summary>CPU / software encoder.</summary>
    Software,

    /// <summary>NVIDIA NVENC.</summary>
    Nvenc,

    /// <summary>Intel Quick Sync.</summary>
    Qsv,

    /// <summary>VA-API (Linux).</summary>
    Vaapi,

    /// <summary>AMD AMF (Windows).</summary>
    Amf,

    /// <summary>Apple VideoToolbox (macOS).</summary>
    VideoToolbox
}
