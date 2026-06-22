using Jellyfin.Plugin.TranscodeReplace.Verify;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class VmafParserTests
{
    [Fact]
    public void Parses_Score_From_Ffmpeg_Output()
    {
        const string output = "[Parsed_libvmaf_0 @ 0x55f] VMAF score: 96.453219\nframe= 1000";
        var score = VmafParser.Parse(output);
        Assert.NotNull(score);
        Assert.Equal(96.45, score!.Value, 2);
    }

    [Fact]
    public void Returns_Null_When_No_Score()
    {
        Assert.Null(VmafParser.Parse("some unrelated ffmpeg output without a score"));
    }
}
