using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Jellyfin.Plugin.TranscodeReplace.Verify;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class VerifierTests
{
    private static readonly PluginConfiguration Config = new() { TargetVideoCodec = TargetVideoCodec.Hevc };

    private static MediaSummary Source(double duration = 100, int audio = 2) =>
        new() { VideoCodec = "h264", DurationSeconds = duration, AudioStreamCount = audio };

    private static MediaSummary Output(string codec = "hevc", double duration = 100, int audio = 2) =>
        new() { VideoCodec = codec, DurationSeconds = duration, AudioStreamCount = audio };

    [Fact]
    public void Passes_When_Smaller_And_Intact()
    {
        var result = Verifier.Check(Source(), Output(), 1000, 500, Config);
        Assert.Equal(VerifyOutcome.Passed, result.Outcome);
    }

    [Fact]
    public void Fails_On_Codec_Mismatch()
    {
        var result = Verifier.Check(Source(), Output(codec: "h264"), 1000, 500, Config);
        Assert.Equal(VerifyOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void Fails_On_Duration_Drift()
    {
        var result = Verifier.Check(Source(duration: 100), Output(duration: 90), 1000, 500, Config);
        Assert.Equal(VerifyOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void Fails_When_Audio_Streams_Lost()
    {
        var result = Verifier.Check(Source(audio: 2), Output(audio: 1), 1000, 500, Config);
        Assert.Equal(VerifyOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void Fails_When_Suspiciously_Small()
    {
        var result = Verifier.Check(Source(), Output(), 1000, 10, Config);
        Assert.Equal(VerifyOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void Fails_When_Output_Probe_Null()
    {
        var result = Verifier.Check(Source(), null, 1000, 500, Config);
        Assert.Equal(VerifyOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void Skips_When_Output_Not_Smaller()
    {
        var result = Verifier.Check(Source(), Output(), 1000, 1200, Config);
        Assert.Equal(VerifyOutcome.SkipNotSmaller, result.Outcome);
    }
}
