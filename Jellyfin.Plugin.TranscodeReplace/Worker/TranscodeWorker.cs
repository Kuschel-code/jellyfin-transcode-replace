using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TranscodeReplace.Worker;

/// <summary>
/// Background worker that drains the job queue. In this build (M3) it only logs
/// the ffmpeg command in dry-run; it never writes media. Live encode, verify and
/// atomic replace are added in later milestones.
/// </summary>
public sealed class TranscodeWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);

    private readonly IJobQueue _queue;
    private readonly HardwareProbe _probe;
    private readonly ArgBuilder _argBuilder;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<TranscodeWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscodeWorker"/> class.
    /// </summary>
    /// <param name="queue">Job queue.</param>
    /// <param name="probe">Hardware probe.</param>
    /// <param name="argBuilder">Argument builder.</param>
    /// <param name="mediaEncoder">Jellyfin media encoder (for the ffmpeg path).</param>
    /// <param name="logger">Logger.</param>
    public TranscodeWorker(
        IJobQueue queue,
        HardwareProbe probe,
        ArgBuilder argBuilder,
        IMediaEncoder mediaEncoder,
        ILogger<TranscodeWorker> logger)
    {
        _queue = queue;
        _probe = probe;
        _argBuilder = argBuilder;
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reset = _queue.ResetInFlight();
        if (reset > 0)
        {
            _logger.LogInformation("Reset {Count} in-flight job(s) to Pending after restart.", reset);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            TranscodeJob? job = null;
            try
            {
                job = _queue.Dequeue();
                if (job is null)
                {
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                ProcessJob(job);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker error processing job {JobId}", job?.Id);
                if (job is not null)
                {
                    job.State = JobState.Failed;
                    job.Error = ex.Message;
                    job.Attempts++;
                    _queue.Update(job);
                }
            }
        }
    }

    private void ProcessJob(TranscodeJob job)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var ffmpegPath = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            MarkSkipped(job, "jellyfin-ffmpeg path is unavailable");
            return;
        }

        var encoder = SelectEncoder(config, ffmpegPath);
        if (encoder is null)
        {
            MarkSkipped(job, $"no usable encoder for target codec {config.TargetVideoCodec}");
            return;
        }

        // M3: a minimal summary. M4 replaces this with a real ffprobe summary.
        var summary = MediaSummary.Minimal(job.SourcePath);
        var result = _argBuilder.Build(job, summary, config, encoder, SoftwarePreset());
        var commandLine = FfmpegRunner.Quote(result.Arguments);

        if (config.DryRun)
        {
            _logger.LogInformation(
                "[DRY-RUN] {Source} | encoder={Encoder} ({Kind}) -> {Temp}{Note}\n  ffmpeg {Cmd}",
                job.SourcePath,
                encoder.Name,
                encoder.Kind,
                result.TempOutputPath,
                result.ContainerFallbackReason is null ? string.Empty : " | note: " + result.ContainerFallbackReason,
                commandLine);

            job.State = JobState.Skipped;
            job.Error = "dry-run (no file written)";
            _queue.Update(job);
            return;
        }

        // Live encode/verify/replace (M4-M5) are not part of this build. Refuse to
        // touch any media rather than run an unverified, irreversible pipeline.
        _logger.LogWarning(
            "DryRun is off but the live encode/verify/replace pipeline is not enabled in this build; skipping {Source}.",
            job.SourcePath);
        MarkSkipped(job, "live pipeline not enabled in this build (still M3)");
    }

    private void MarkSkipped(TranscodeJob job, string reason)
    {
        job.State = JobState.Skipped;
        job.Error = reason;
        _queue.Update(job);
    }

    private EncoderCap? SelectEncoder(PluginConfiguration config, string ffmpegPath)
    {
        var usable = _probe.Usable(ffmpegPath)
            .Where(e => e.Codec == config.TargetVideoCodec)
            .ToList();

        if (usable.Count == 0)
        {
            return null;
        }

        if (config.EncoderPreference == EncoderPreferenceMode.Specific &&
            !string.IsNullOrWhiteSpace(config.SpecificEncoder))
        {
            return usable.FirstOrDefault(e =>
                string.Equals(e.Name, config.SpecificEncoder, StringComparison.OrdinalIgnoreCase));
        }

        IEnumerable<EncoderCap> ordered = config.EncoderPreference switch
        {
            EncoderPreferenceMode.ForceSoftware => usable.Where(e => e.Kind == HwKind.Software),
            EncoderPreferenceMode.ForceHardware => usable.OrderByDescending(e => e.Kind != HwKind.Software),
            _ => config.Mode == QualityVsSpeed.Quality
                ? usable.OrderByDescending(e => e.Kind == HwKind.Software)
                : usable.OrderByDescending(e => e.Kind != HwKind.Software)
        };

        return ordered.FirstOrDefault();
    }

    private static string SoftwarePreset() => Environment.ProcessorCount >= 16 ? "medium" : "slow";
}
