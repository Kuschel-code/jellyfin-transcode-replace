using System.Linq;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class HardwareProbeTests
{
    private const string Sample =
        "Encoders:\n" +
        " V..... = Video\n" +
        " A..... = Audio\n" +
        " ------\n" +
        " V....D libx264              libx264 H.264 / AVC\n" +
        " V....D libx265              libx265 H.265 / HEVC\n" +
        " V....D hevc_nvenc           NVIDIA NVENC hevc encoder\n" +
        " A....D aac                  AAC (Advanced Audio Coding)\n";

    [Fact]
    public void ParseEncoderNames_Extracts_Video_Encoders_Only()
    {
        var names = HardwareProbe.ParseEncoderNames(Sample);

        Assert.Contains("libx264", names);
        Assert.Contains("libx265", names);
        Assert.Contains("hevc_nvenc", names);
        Assert.DoesNotContain("aac", names);
        Assert.DoesNotContain("=", names);
    }

    [Fact]
    public void Detect_Marks_Encoder_Usable_Only_When_Probe_Succeeds()
    {
        var runner = new FakeProcessRunner((_, args) =>
        {
            if (args.Contains("-encoders"))
            {
                return new ProcessResult(0, Sample, string.Empty);
            }

            // Probe-encode: only libx265 "works" on this fake machine.
            return args.Contains("-c:v libx265")
                ? new ProcessResult(0, string.Empty, string.Empty)
                : new ProcessResult(1, string.Empty, "device unavailable");
        });

        var probe = new HardwareProbe(runner);
        var caps = probe.Detect("ffmpeg");

        var x265 = caps.First(c => c.Name == "libx265");
        Assert.True(x265.Present);
        Assert.True(x265.Usable);

        var nvenc = caps.First(c => c.Name == "hevc_nvenc");
        Assert.True(nvenc.Present);
        Assert.False(nvenc.Usable);

        Assert.Contains(probe.Usable("ffmpeg"), c => c.Name == "libx265");
        Assert.DoesNotContain(probe.Usable("ffmpeg"), c => c.Name == "hevc_nvenc");
    }
}
