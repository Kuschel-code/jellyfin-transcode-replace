using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class ContainerLogicTests
{
    [Fact]
    public void Keep_Returns_Source_Container()
    {
        var decision = ContainerLogic.Resolve(ContainerPreference.Keep, new MediaSummary { Container = "mkv" });
        Assert.Equal("mkv", decision.Extension);
        Assert.False(decision.FastStart);
    }

    [Fact]
    public void Keep_Normalizes_MatroskaWebm()
    {
        var decision = ContainerLogic.Resolve(ContainerPreference.Keep, new MediaSummary { Container = "matroska,webm" });
        Assert.Equal("mkv", decision.Extension);
    }

    [Fact]
    public void Mp4_Without_Image_Subs_Enables_Faststart()
    {
        var decision = ContainerLogic.Resolve(ContainerPreference.Mp4, new MediaSummary { Container = "mkv" });
        Assert.Equal("mp4", decision.Extension);
        Assert.True(decision.FastStart);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void Mp4_With_Pgs_Falls_Back_To_Mkv()
    {
        var source = new MediaSummary { Container = "mkv", SubtitleCodecs = new[] { "hdmv_pgs_subtitle" } };
        var decision = ContainerLogic.Resolve(ContainerPreference.Mp4, source);
        Assert.Equal("mkv", decision.Extension);
        Assert.False(decision.FastStart);
        Assert.NotNull(decision.Reason);
    }
}
