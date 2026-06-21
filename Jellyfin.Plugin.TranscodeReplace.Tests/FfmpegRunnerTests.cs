using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class FfmpegRunnerTests
{
    [Fact]
    public void Quote_Leaves_Simple_Args_Unquoted()
    {
        Assert.Equal("a b c", FfmpegRunner.Quote(new[] { "a", "b", "c" }));
    }

    [Fact]
    public void Quote_Wraps_Args_With_Spaces()
    {
        Assert.Equal("\"a b\"", FfmpegRunner.Quote(new[] { "a b" }));
    }

    [Fact]
    public void Quote_Quotes_Paths_With_Spaces()
    {
        var quoted = FfmpegRunner.Quote(new[] { "-i", "/media/My Movie.mkv" });
        Assert.Contains("\"/media/My Movie.mkv\"", quoted);
    }
}
