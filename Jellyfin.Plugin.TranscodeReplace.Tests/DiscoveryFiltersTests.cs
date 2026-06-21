using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Discovery;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class DiscoveryFiltersTests
{
    [Theory]
    [InlineData("hevc", false)]   // already target -> skip
    [InlineData("h265", false)]   // alias of hevc -> skip
    [InlineData("h264", true)]    // different codec -> process
    [InlineData(null, true)]      // unknown -> process
    public void QualifiesByCodec_Respects_SkipIfAlreadyTargetCodec(string? codec, bool expected)
    {
        var config = new PluginConfiguration { TargetVideoCodec = TargetVideoCodec.Hevc, SkipIfAlreadyTargetCodec = true };
        Assert.Equal(expected, DiscoveryFilters.QualifiesByCodec(codec, config));
    }

    [Fact]
    public void QualifiesByCodec_True_When_Skip_Disabled()
    {
        var config = new PluginConfiguration { TargetVideoCodec = TargetVideoCodec.Hevc, SkipIfAlreadyTargetCodec = false };
        Assert.True(DiscoveryFilters.QualifiesByCodec("hevc", config));
    }

    [Theory]
    [InlineData(6000, true)]
    [InlineData(3000, false)]
    [InlineData(null, true)] // unknown bitrate qualifies
    public void QualifiesByBitrate_Honors_Minimum(int? bitrateKbps, bool expected)
    {
        var config = new PluginConfiguration { MinSourceBitrateKbps = 5000 };
        Assert.Equal(expected, DiscoveryFilters.QualifiesByBitrate(bitrateKbps, config));
    }

    [Fact]
    public void PassesPathFilters_Exclude_Wins()
    {
        var config = new PluginConfiguration { PathExcludeGlobs = new[] { "**/Sample/**" } };
        Assert.False(DiscoveryFilters.PassesPathFilters("/media/Movies/Sample/A.mkv", config));
        Assert.True(DiscoveryFilters.PassesPathFilters("/media/Movies/A.mkv", config));
    }

    [Fact]
    public void PassesPathFilters_Include_Requires_Match()
    {
        var config = new PluginConfiguration { PathIncludeGlobs = new[] { "**/*.mkv" } };
        Assert.True(DiscoveryFilters.PassesPathFilters("/media/Movies/A.mkv", config));
        Assert.False(DiscoveryFilters.PassesPathFilters("/media/Movies/A.mp4", config));
    }
}
