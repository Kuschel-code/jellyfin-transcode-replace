using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;

namespace Jellyfin.Plugin.TranscodeReplace.Encoding;

/// <summary>The result of building an ffmpeg invocation.</summary>
/// <param name="Arguments">Ordered argument list.</param>
/// <param name="TempOutputPath">Temp output path (same directory as source).</param>
/// <param name="FinalExtension">Chosen output extension.</param>
/// <param name="ContainerFallbackReason">Reason a container fallback occurred, if any.</param>
public sealed record BuildResult(
    IReadOnlyList<string> Arguments,
    string TempOutputPath,
    string FinalExtension,
    string? ContainerFallbackReason);

/// <summary>
/// Builds deterministic ffmpeg argument lists per encoder / HDR / container
/// combination (architecture plan section 3.5). Pure: no IO, no processes.
/// </summary>
public sealed class ArgBuilder
{
    /// <summary>Builds the ffmpeg invocation for a job.</summary>
    /// <param name="job">The job.</param>
    /// <param name="source">Source media summary.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="encoder">Chosen encoder.</param>
    /// <param name="softwarePreset">Software preset to use for SW encoders.</param>
    /// <returns>The build result.</returns>
    public BuildResult Build(
        TranscodeJob job,
        MediaSummary source,
        PluginConfiguration config,
        EncoderCap encoder,
        string softwarePreset)
    {
        var container = ContainerLogic.Resolve(config.PreferredContainer, source);
        var ext = container.Extension;

        var directory = Path.GetDirectoryName(job.SourcePath) ?? string.Empty;
        var fileName = Path.GetFileName(job.SourcePath);
        var tempPath = Path.Combine(
            directory,
            string.Format(CultureInfo.InvariantCulture, "{0}.tmp-{1:N}.{2}", fileName, job.Id, ext));

        var tenBit = source.Is10Bit || source.Hdr is HdrType.Hdr10 or HdrType.Hlg;
        var plan = EncoderMatrix.Build(encoder, config.QualityValue, tenBit, softwarePreset);

        var args = new List<string> { "-hide_banner", "-y" };

        if (encoder.Kind == HwKind.Vaapi)
        {
            args.Add("-vaapi_device");
            args.Add("/dev/dri/renderD128");
        }

        args.Add("-i");
        args.Add(job.SourcePath);

        if (encoder.Kind == HwKind.Vaapi)
        {
            args.Add("-vf");
            args.Add("format=nv12|p010,hwupload");
        }

        args.Add("-map");
        args.Add("0");

        args.Add("-c:v");
        args.Add(encoder.Name);
        args.AddRange(plan.QualityArgs);
        args.Add("-pix_fmt");
        args.Add(plan.PixelFormat);

        AppendHdr(args, source, encoder);
        AppendAudio(args, config.AudioMode, source.AudioStreamCount);
        AppendSubtitles(args, ext);

        args.Add("-map_metadata");
        args.Add("0");
        args.Add("-map_chapters");
        args.Add("0");

        if (container.FastStart)
        {
            args.Add("-movflags");
            args.Add("+faststart");
        }

        args.Add(tempPath);

        return new BuildResult(args, tempPath, ext, container.Reason);
    }

    private static void AppendHdr(List<string> args, MediaSummary source, EncoderCap encoder)
    {
        if (source.Hdr is not (HdrType.Hdr10 or HdrType.Hlg))
        {
            return;
        }

        args.Add("-colorspace");
        args.Add("bt2020nc");
        args.Add("-color_primaries");
        args.Add("bt2020");
        args.Add("-color_trc");
        args.Add(source.Hdr == HdrType.Hdr10 ? "smpte2084" : "arib-std-b67");

        if (encoder.Name == "libx265")
        {
            args.Add("-x265-params");
            args.Add("hdr10=1:repeat-headers=1");
        }
    }

    private static void AppendAudio(List<string> args, AudioHandling mode, int audioStreamCount)
    {
        switch (mode)
        {
            case AudioHandling.Transcode:
                args.Add("-c:a");
                args.Add("aac");
                args.Add("-b:a");
                args.Add("256k");
                break;

            case AudioHandling.AddAac:
                // Keep all originals, append a single AAC compatibility track.
                args.Add("-c:a");
                args.Add("copy");
                args.Add("-map");
                args.Add("0:a:0?");
                args.Add(string.Format(CultureInfo.InvariantCulture, "-c:a:{0}", audioStreamCount));
                args.Add("aac");
                args.Add(string.Format(CultureInfo.InvariantCulture, "-b:a:{0}", audioStreamCount));
                args.Add("256k");
                break;

            case AudioHandling.Copy:
            default:
                args.Add("-c:a");
                args.Add("copy");
                break;
        }
    }

    private static void AppendSubtitles(List<string> args, string ext)
    {
        args.Add("-c:s");
        args.Add(ext == "mp4" ? "mov_text" : "copy");
    }
}
