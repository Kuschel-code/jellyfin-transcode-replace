using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;

namespace Jellyfin.Plugin.TranscodeReplace.Discovery;

/// <summary>
/// Pure decision helpers used by the discovery task to decide whether a file
/// should be enqueued. Kept free of Jellyfin types for unit testing.
/// </summary>
public static class DiscoveryFilters
{
    /// <summary>
    /// Whether a file qualifies by codec. A file already in the target codec is
    /// rejected when <see cref="PluginConfiguration.SkipIfAlreadyTargetCodec"/> is on.
    /// </summary>
    /// <param name="videoCodec">Source video codec.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>True if it qualifies.</returns>
    public static bool QualifiesByCodec(string? videoCodec, PluginConfiguration config) =>
        !(config.SkipIfAlreadyTargetCodec && CodecNames.Matches(videoCodec, config.TargetVideoCodec));

    /// <summary>
    /// Whether a file qualifies by bitrate. Unknown bitrate always qualifies.
    /// </summary>
    /// <param name="bitrateKbps">Source video bitrate in kbps, if known.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>True if it qualifies.</returns>
    public static bool QualifiesByBitrate(int? bitrateKbps, PluginConfiguration config) =>
        config.MinSourceBitrateKbps <= 0 || (bitrateKbps ?? int.MaxValue) >= config.MinSourceBitrateKbps;

    /// <summary>
    /// Whether a path passes include/exclude globs. Exclude wins; when includes
    /// are present at least one must match.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>True if the path passes.</returns>
    public static bool PassesPathFilters(string path, PluginConfiguration config)
    {
        if (config.PathExcludeGlobs.Any(g => GlobMatcher.IsMatch(path, g)))
        {
            return false;
        }

        if (config.PathIncludeGlobs.Length > 0 && !config.PathIncludeGlobs.Any(g => GlobMatcher.IsMatch(path, g)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether an item passes the library scope. An empty include list means all
    /// libraries are allowed; otherwise the item must belong to one of them.
    /// </summary>
    /// <param name="itemLibraryIds">Library (collection folder) ids the item belongs to.</param>
    /// <param name="includedLibraryIds">Configured included library ids (strings).</param>
    /// <returns>True if the item is in scope.</returns>
    public static bool PassesLibraryScope(IReadOnlyCollection<Guid> itemLibraryIds, string[] includedLibraryIds)
    {
        if (includedLibraryIds is null || includedLibraryIds.Length == 0)
        {
            return true;
        }

        var allowed = includedLibraryIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToHashSet();

        // A non-empty configured list is an explicit restriction: honour it strictly.
        // If nothing parsed, match nothing (fail closed) rather than processing every file.
        return itemLibraryIds.Any(allowed.Contains);
    }
}
