namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>HDR classification of a source (architecture plan section 3.6).</summary>
public enum HdrType
{
    /// <summary>Standard dynamic range.</summary>
    None,

    /// <summary>HDR10 (PQ / smpte2084).</summary>
    Hdr10,

    /// <summary>Hybrid Log-Gamma (arib-std-b67).</summary>
    Hlg,

    /// <summary>Dolby Vision (generically not preservable; default skip).</summary>
    DolbyVision
}
