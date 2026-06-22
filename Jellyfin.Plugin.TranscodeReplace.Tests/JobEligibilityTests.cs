using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Jellyfin.Plugin.TranscodeReplace.Verify;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class JobEligibilityTests
{
    [Fact]
    public void Skips_Dolby_Vision_When_Configured()
    {
        var source = new MediaSummary { IsDolbyVision = true };
        var (skip, reason) = JobEligibility.Evaluate(source, new PluginConfiguration { SkipDolbyVision = true });
        Assert.True(skip);
        Assert.NotNull(reason);
    }

    [Fact]
    public void Skips_Hdr_When_Configured()
    {
        var source = new MediaSummary { ColorTransfer = "smpte2084" };
        var (skip, _) = JobEligibility.Evaluate(source, new PluginConfiguration { SkipHdr = true });
        Assert.True(skip);
    }

    [Fact]
    public void Does_Not_Skip_Hdr_When_Allowed()
    {
        var source = new MediaSummary { ColorTransfer = "smpte2084" };
        var (skip, _) = JobEligibility.Evaluate(source, new PluginConfiguration { SkipHdr = false, SkipDolbyVision = true });
        Assert.False(skip);
    }

    [Fact]
    public void Does_Not_Skip_Sdr()
    {
        var source = new MediaSummary { ColorTransfer = "bt709" };
        var (skip, _) = JobEligibility.Evaluate(source, new PluginConfiguration { SkipHdr = true, SkipDolbyVision = true });
        Assert.False(skip);
    }
}
