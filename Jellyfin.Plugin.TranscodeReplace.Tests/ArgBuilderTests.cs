using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class ArgBuilderTests
{
    private static readonly Guid FixedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly EncoderCap SwHevc = new("libx265", HwKind.Software, TargetVideoCodec.Hevc, true, true);
    private readonly ArgBuilder _builder = new();

    private static TranscodeJob Job(string path) => new() { Id = FixedId, SourcePath = path };

    private static void AssertSeq(IReadOnlyList<string> args, params string[] expected) =>
        Assert.Contains(string.Join(' ', expected), string.Join(' ', args));

    [Fact]
    public void Software_Hevc_Sdr_Copy_Mkv()
    {
        var result = _builder.Build(Job("/media/Movie.mkv"), MediaSummary.Minimal("/media/Movie.mkv"),
            new PluginConfiguration(), SwHevc, "slow");

        AssertSeq(result.Arguments, "-c:v", "libx265", "-crf", "23", "-preset", "slow", "-pix_fmt", "yuv420p");
        AssertSeq(result.Arguments, "-c:a", "copy");
        AssertSeq(result.Arguments, "-c:s", "copy");
        Assert.Equal("mkv", result.FinalExtension);
        Assert.EndsWith(".mkv", result.TempOutputPath);
        Assert.Contains(".tmp-", result.TempOutputPath);
        Assert.Null(result.ContainerFallbackReason);
    }

    [Fact]
    public void Nvenc_Hevc_Uses_Cq_And_Nv12()
    {
        var nvenc = new EncoderCap("hevc_nvenc", HwKind.Nvenc, TargetVideoCodec.Hevc, true, true);

        var result = _builder.Build(Job("/media/Movie.mkv"), MediaSummary.Minimal("/media/Movie.mkv"),
            new PluginConfiguration(), nvenc, "slow");

        AssertSeq(result.Arguments, "-c:v", "hevc_nvenc", "-preset", "p5", "-cq", "23", "-pix_fmt", "nv12");
    }

    [Fact]
    public void Hdr10_Software_Preserves_Color_Metadata_And_10bit()
    {
        var source = new MediaSummary
        {
            SourcePath = "/media/Hdr.mkv",
            Container = "mkv",
            ColorTransfer = "smpte2084",
            Is10Bit = true,
            AudioStreamCount = 1
        };

        var result = _builder.Build(Job("/media/Hdr.mkv"), source, new PluginConfiguration(), SwHevc, "slow");

        AssertSeq(result.Arguments, "-pix_fmt", "yuv420p10le");
        AssertSeq(result.Arguments, "-colorspace", "bt2020nc", "-color_primaries", "bt2020", "-color_trc", "smpte2084");
        AssertSeq(result.Arguments, "-x265-params", "hdr10=1:repeat-headers=1");
    }

    [Fact]
    public void Mp4_With_Image_Subs_Falls_Back_To_Mkv()
    {
        var source = new MediaSummary
        {
            Container = "mkv",
            SubtitleCodecs = new[] { "hdmv_pgs_subtitle" },
            AudioStreamCount = 1
        };
        var config = new PluginConfiguration { PreferredContainer = ContainerPreference.Mp4 };

        var result = _builder.Build(Job("/media/Movie.mkv"), source, config, SwHevc, "slow");

        Assert.Equal("mkv", result.FinalExtension);
        Assert.NotNull(result.ContainerFallbackReason);
        AssertSeq(result.Arguments, "-c:s", "copy");
    }

    [Fact]
    public void Mp4_Without_Image_Subs_Uses_Faststart_And_MovText()
    {
        var source = new MediaSummary { Container = "mkv", AudioStreamCount = 1 };
        var config = new PluginConfiguration { PreferredContainer = ContainerPreference.Mp4 };

        var result = _builder.Build(Job("/media/Movie.mkv"), source, config, SwHevc, "slow");

        Assert.Equal("mp4", result.FinalExtension);
        AssertSeq(result.Arguments, "-c:s", "mov_text");
        AssertSeq(result.Arguments, "-movflags", "+faststart");
    }

    [Fact]
    public void Vaapi_Adds_Device_Before_Input_And_Upload_Filter()
    {
        var vaapi = new EncoderCap("hevc_vaapi", HwKind.Vaapi, TargetVideoCodec.Hevc, true, true);

        var result = _builder.Build(Job("/media/Movie.mkv"), MediaSummary.Minimal("/media/Movie.mkv"),
            new PluginConfiguration(), vaapi, "slow");

        AssertSeq(result.Arguments, "-vaapi_device", "/dev/dri/renderD128", "-i", "/media/Movie.mkv",
            "-vf", "format=nv12|p010,hwupload");
        AssertSeq(result.Arguments, "-qp", "23");
    }

    [Fact]
    public void Audio_Transcode_Mode_Emits_Aac()
    {
        var config = new PluginConfiguration { AudioMode = AudioHandling.Transcode };

        var result = _builder.Build(Job("/media/Movie.mkv"), MediaSummary.Minimal("/media/Movie.mkv"),
            config, SwHevc, "slow");

        AssertSeq(result.Arguments, "-c:a", "aac", "-b:a", "256k");
    }
}
