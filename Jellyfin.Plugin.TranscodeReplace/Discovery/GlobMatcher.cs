using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.TranscodeReplace.Discovery;

/// <summary>
/// Minimal glob matcher supporting <c>*</c>, <c>?</c> and <c>**</c>. Paths are
/// normalised to forward slashes and matched case-insensitively. Pure function.
/// </summary>
public static class GlobMatcher
{
    /// <summary>Matches a path against a glob pattern.</summary>
    /// <param name="path">The path to test.</param>
    /// <param name="glob">The glob pattern.</param>
    /// <returns>True if the path matches.</returns>
    public static bool IsMatch(string path, string glob)
    {
        if (string.IsNullOrEmpty(glob))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        var escaped = Regex.Escape(glob.Replace('\\', '/'));

        var pattern = "^" + escaped
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]") + "$";

        return Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase);
    }
}
