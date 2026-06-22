using Jellyfin.Plugin.TranscodeReplace.Configuration;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>The resolved output container decision.</summary>
/// <param name="Extension">Chosen file extension (no dot).</param>
/// <param name="FastStart">Whether to add <c>+faststart</c> (mp4 only).</param>
/// <param name="Reason">Reason for a fallback, if any.</param>
public sealed record ContainerDecision(string Extension, bool FastStart, string? Reason);

/// <summary>
/// Decides the output container, falling back from mp4 to mkv when the source
/// carries image-based subtitles mp4 cannot hold (architecture plan section 3.5).
/// Pure logic.
/// </summary>
public static class ContainerLogic
{
    /// <summary>Resolves the output container.</summary>
    /// <param name="preference">Configured preference.</param>
    /// <param name="source">Source summary.</param>
    /// <returns>The container decision.</returns>
    public static ContainerDecision Resolve(ContainerPreference preference, MediaSummary source)
    {
        var ext = preference switch
        {
            ContainerPreference.Mkv => "mkv",
            ContainerPreference.Mp4 => "mp4",
            ContainerPreference.Webm => "webm",
            _ => Normalize(source.Container)
        };

        // Neither mp4 nor webm can carry image-based subtitles (PGS/VobSub); fall
        // back to mkv rather than silently dropping the subtitle streams.
        string? reason = null;
        if ((ext == "mp4" || ext == "webm") && source.HasImageSubtitles)
        {
            reason = $"Source has image-based subtitles (PGS/VobSub); {ext} cannot carry them, kept mkv.";
            ext = "mkv";
        }

        return new ContainerDecision(ext, ext == "mp4", reason);
    }

    private static string Normalize(string container)
    {
        var c = container.Trim().TrimStart('.').ToLowerInvariant();
        return c switch
        {
            "" => "mkv",
            "matroska" => "mkv",
            "x-matroska" => "mkv",
            "matroska,webm" => "mkv",
            _ => c
        };
    }
}
