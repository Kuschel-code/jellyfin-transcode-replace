using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;

namespace Jellyfin.Plugin.TranscodeReplace.Verify;

/// <summary>
/// Decides whether a probed source should be skipped for HDR/Dolby Vision reasons
/// (plan section 3.6). Pure logic. Never silently degrades HDR to SDR — it skips.
/// </summary>
public static class JobEligibility
{
    /// <summary>Evaluates skip conditions.</summary>
    /// <param name="source">Probed source summary.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>Whether to skip, and why.</returns>
    public static (bool Skip, string? Reason) Evaluate(MediaSummary source, PluginConfiguration config)
    {
        if (config.SkipDolbyVision && source.Hdr == HdrType.DolbyVision)
        {
            return (true, "Dolby Vision source (skip configured) — not generically preservable");
        }

        if (config.SkipHdr && source.Hdr is HdrType.Hdr10 or HdrType.Hlg)
        {
            return (true, "HDR source (skip configured)");
        }

        return (false, null);
    }
}
