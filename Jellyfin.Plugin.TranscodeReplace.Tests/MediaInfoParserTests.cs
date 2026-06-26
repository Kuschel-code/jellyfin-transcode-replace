using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class MediaInfoParserTests
{
    private const string Hdr10Json = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "hevc",
          "pix_fmt": "yuv420p10le",
          "profile": "Main 10",
          "color_transfer": "smpte2084",
          "color_primaries": "bt2020",
          "color_space": "bt2020nc",
          "bit_rate": "12000000"
        },
        { "codec_type": "audio", "codec_name": "eac3" },
        { "codec_type": "audio", "codec_name": "aac" },
        { "codec_type": "subtitle", "codec_name": "hdmv_pgs_subtitle" }
      ],
      "format": { "duration": "5400.000000", "bit_rate": "13000000" }
    }
    """;

    private const string DolbyVisionJson = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "hevc",
          "pix_fmt": "yuv420p10le",
          "side_data_list": [ { "side_data_type": "DOVI configuration record" } ]
        }
      ],
      "format": { "duration": "1200.0" }
    }
    """;

    [Fact]
    public void Parses_Hdr10_Streams_And_Format()
    {
        var summary = MediaInfoParser.Parse(Hdr10Json, "/media/Movie.mkv");

        Assert.Equal("hevc", summary.VideoCodec);
        Assert.True(summary.Is10Bit);
        Assert.Equal(HdrType.Hdr10, summary.Hdr);
        Assert.Equal(2, summary.AudioStreamCount);
        Assert.Contains("hdmv_pgs_subtitle", summary.SubtitleCodecs);
        Assert.True(summary.HasImageSubtitles);
        Assert.False(summary.IsDolbyVision);
        Assert.Equal(5400, summary.DurationSeconds!.Value, 1);
        Assert.Equal(12000, summary.VideoBitrateKbps);
        Assert.Equal("mkv", summary.Container);
    }

    [Fact]
    public void Detects_Dolby_Vision_From_Side_Data()
    {
        var summary = MediaInfoParser.Parse(DolbyVisionJson, "/media/DV.mp4");

        Assert.True(summary.IsDolbyVision);
        Assert.Equal(HdrType.DolbyVision, summary.Hdr);
        Assert.Equal("mp4", summary.Container);
    }

    [Theory]
    [InlineData("yuv410p", false)]      // 8-bit 4:1:0 — contains "10" but is NOT 10-bit
    [InlineData("yuv420p", false)]
    [InlineData("nv12", false)]
    [InlineData("yuv420p10le", true)]
    [InlineData("p010le", true)]
    public void Detects_10bit_From_PixFmt_Without_False_Positives(string pixFmt, bool expected)
    {
        var json = $$"""
        { "streams": [ { "codec_type": "video", "codec_name": "h264", "pix_fmt": "{{pixFmt}}" } ], "format": {} }
        """;

        var summary = MediaInfoParser.Parse(json, "/media/Clip.mkv");

        Assert.Equal(expected, summary.Is10Bit);
    }
}
