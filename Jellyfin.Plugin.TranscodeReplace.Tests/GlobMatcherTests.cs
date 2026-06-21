using Jellyfin.Plugin.TranscodeReplace.Discovery;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class GlobMatcherTests
{
    [Theory]
    [InlineData("A.mkv", "*.mkv", true)]
    [InlineData("A.mp4", "*.mkv", false)]
    [InlineData("/media/Movies/A.mkv", "**/*.mkv", true)]
    [InlineData("/media/Movies/A.mp4", "**/*.mkv", false)]
    [InlineData("/media/Movies/Sample/A.mkv", "**/Sample/**", true)]
    [InlineData("/media/Movies/A.mkv", "**/Sample/**", false)]
    [InlineData("A.mkv", "", false)]
    [InlineData("", "*.mkv", false)]
    public void IsMatch_Matches_Expected(string path, string glob, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, glob));
    }
}
