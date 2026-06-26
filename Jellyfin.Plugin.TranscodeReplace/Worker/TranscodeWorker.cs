using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using Jellyfin.Plugin.TranscodeReplace.Replace;
using Jellyfin.Plugin.TranscodeReplace.Verify;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TranscodeReplace.Worker;

/// <summary>
/// Background worker that drains the job queue and runs the full pipeline:
/// probe, encode, verify, optional VMAF gate, then atomic replace. Processes one job
/// at a time. The original is only ever touched after every gate passes.
/// </summary>
public sealed class TranscodeWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(15);

    private readonly IJobQueue _queue;
    private readonly HardwareProbe _probe;
    private readonly ArgBuilder _argBuilder;
    private readonly FfmpegRunner _ffmpeg;
    private readonly MediaInfoProbe _mediaProbe;
    private readonly VmafGate _vmaf;
    private readonly AtomicReplacer _replacer;
    private readonly PlaybackGuard _playbackGuard;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<TranscodeWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscodeWorker"/> class.
    /// </summary>
    /// <param name="queue">Job queue.</param>
    /// <param name="probe">Hardware probe.</param>
    /// <param name="argBuilder">Argument builder.</param>
    /// <param name="ffmpeg">ffmpeg runner.</param>
    /// <param name="mediaProbe">ffprobe wrapper.</param>
    /// <param name="vmaf">VMAF gate.</param>
    /// <param name="replacer">Atomic replacer.</param>
    /// <param name="playbackGuard">Playback guard.</param>
    /// <param name="mediaEncoder">Jellyfin media encoder (ffmpeg/ffprobe paths).</param>
    /// <param name="logger">Logger.</param>
    public TranscodeWorker(
        IJobQueue queue,
        HardwareProbe probe,
        ArgBuilder argBuilder,
        FfmpegRunner ffmpeg,
        MediaInfoProbe mediaProbe,
        VmafGate vmaf,
        AtomicReplacer replacer,
        PlaybackGuard playbackGuard,
        IMediaEncoder mediaEncoder,
        ILogger<TranscodeWorker> logger)
    {
        _queue = queue;
        _probe = probe;
        _argBuilder = argBuilder;
        _ffmpeg = ffmpeg;
        _mediaProbe = mediaProbe;
        _vmaf = vmaf;
        _replacer = replacer;
        _playbackGuard = playbackGuard;
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RecoverOrphanedBackups();

        var reset = _queue.ResetInFlight();
        if (reset > 0)
        {
            _logger.LogInformation("Reset {Count} in-flight job(s) to Pending after restart.", reset);
        }

        CleanupStaleTemps();

        while (!stoppingToken.IsCancellationRequested)
        {
            TranscodeJob? job = null;
            try
            {
                var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

                if (config.RunOnlyWhenIdle && _playbackGuard.IsAnythingPlaying())
                {
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                job = _queue.Dequeue();
                if (job is null)
                {
                    CleanupExpiredBackups(config);
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (_playbackGuard.IsFileInUse(job.SourcePath))
                {
                    _logger.LogInformation("Deferring {Source}: currently being streamed.", job.SourcePath);
                    job.State = JobState.Pending;
                    _queue.Update(job);
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await ProcessJobAsync(job, config, stoppingToken).ConfigureAwait(false);
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
                    job.Attempts++;
                    Finish(job, JobState.Failed, ex.Message);
                }
            }
        }
    }

    private async Task ProcessJobAsync(TranscodeJob job, PluginConfiguration config, CancellationToken cancellationToken)
    {
        var ffmpegPath = _mediaEncoder.EncoderPath;
        var ffprobePath = _mediaEncoder.ProbePath;
        if (string.IsNullOrEmpty(ffmpegPath) || string.IsNullOrEmpty(ffprobePath))
        {
            Finish(job, JobState.Skipped, "jellyfin-ffmpeg path is unavailable");
            return;
        }

        if (!File.Exists(job.SourcePath))
        {
            Finish(job, JobState.Skipped, "source file no longer exists");
            return;
        }

        var summary = await _mediaProbe.ProbeAsync(ffprobePath, job.SourcePath, cancellationToken).ConfigureAwait(false);
        if (summary is null)
        {
            Finish(job, JobState.Failed, "ffprobe failed on source (corrupt or unreadable)");
            return;
        }

        job.SourceCodec = summary.VideoCodec;

        // Idempotency: if the source is already the target codec, skip. This also makes
        // a job that was re-queued after a crash mid-replace self-correcting — the
        // already-transcoded file probes as the target codec and is not re-encoded.
        if (config.SkipIfAlreadyTargetCodec && CodecNames.Matches(summary.VideoCodec, config.TargetVideoCodec))
        {
            Finish(job, JobState.Skipped, $"already in target codec ({summary.VideoCodec})");
            return;
        }

        var (skip, skipReason) = JobEligibility.Evaluate(summary, config);
        if (skip)
        {
            Finish(job, JobState.Skipped, skipReason);
            return;
        }

        var encoder = SelectEncoder(config, ffmpegPath);
        if (encoder is null)
        {
            Finish(job, JobState.Skipped, $"no usable encoder for target codec {config.TargetVideoCodec}");
            return;
        }

        var sourceSize = new FileInfo(job.SourcePath).Length;
        if (!DiskSpace.HasRoomFor(job.SourcePath, sourceSize + (sourceSize / 10)))
        {
            Finish(job, JobState.Skipped, "insufficient free disk space for a temp copy");
            return;
        }

        var build = _argBuilder.Build(job, summary, config, encoder, SoftwarePreset());
        var commandLine = FfmpegRunner.Quote(build.Arguments);

        if (config.DryRun)
        {
            _logger.LogInformation(
                "[DRY-RUN] {Source} | {Encoder} ({Kind}) -> {Temp}{Note}\n  ffmpeg {Cmd}",
                job.SourcePath, encoder.Name, encoder.Kind, build.TempOutputPath,
                build.ContainerFallbackReason is null ? string.Empty : " | note: " + build.ContainerFallbackReason,
                commandLine);
            Finish(job, JobState.Skipped, "dry-run (no file written)");
            return;
        }

        _logger.LogInformation("Encoding {Source} with {Encoder} ({Kind}).", job.SourcePath, encoder.Name, encoder.Kind);

        var encodeResult = await _ffmpeg.RunAsync(ffmpegPath, build.Arguments, cancellationToken).ConfigureAwait(false);
        if (encodeResult.ExitCode != 0)
        {
            TryDelete(build.TempOutputPath);
            job.Attempts++;
            Finish(job, JobState.Failed, $"ffmpeg exited with code {encodeResult.ExitCode}");
            return;
        }

        job.State = JobState.Verifying;
        _queue.Update(job);

        var outputSummary = await _mediaProbe.ProbeAsync(ffprobePath, build.TempOutputPath, cancellationToken).ConfigureAwait(false);
        job.OutputCodec = outputSummary?.VideoCodec;
        var outputSize = File.Exists(build.TempOutputPath) ? new FileInfo(build.TempOutputPath).Length : 0;
        var verification = Verifier.Check(summary, outputSummary, sourceSize, outputSize, config);

        if (verification.Outcome == VerifyOutcome.Failed)
        {
            TryDelete(build.TempOutputPath);
            job.Attempts++;
            Finish(job, JobState.Failed, "verification failed: " + verification.Reason);
            return;
        }

        if (verification.Outcome == VerifyOutcome.SkipNotSmaller)
        {
            TryDelete(build.TempOutputPath);
            Finish(job, JobState.Skipped, verification.Reason);
            return;
        }

        if (config.EnableVmafGate)
        {
            var score = await _vmaf.ScoreAsync(ffmpegPath, build.TempOutputPath, job.SourcePath, cancellationToken).ConfigureAwait(false);
            job.Vmaf = score;

            // Fail closed: a quality gate that the user explicitly enabled must not be
            // skipped just because the score could not be computed (libvmaf missing,
            // ffmpeg error, resolution mismatch). Keep the original, discard the output.
            if (score is null)
            {
                TryDelete(build.TempOutputPath);
                job.Attempts++;
                Finish(job, JobState.Failed, "VMAF gate is enabled but the score could not be computed (is libvmaf available in jellyfin-ffmpeg?)");
                return;
            }

            if (score < config.VmafThreshold)
            {
                TryDelete(build.TempOutputPath);
                job.Attempts++;
                Finish(job, JobState.Failed, $"VMAF {score:F2} below threshold {config.VmafThreshold:F2}");
                return;
            }
        }

        job.State = JobState.Replacing;
        _queue.Update(job);

        var replaceResult = _replacer.Replace(job, build.TempOutputPath, config);
        if (!replaceResult.Success)
        {
            TryDelete(build.TempOutputPath);
            job.Attempts++;
            Finish(job, JobState.Failed, "replace failed: " + replaceResult.Error);
            return;
        }

        job.OutputSize = replaceResult.OutputSize;
        job.BackupPath = replaceResult.BackupPath;
        var savedBytes = Math.Max(0, sourceSize - replaceResult.OutputSize);
        _logger.LogInformation(
            "Replaced {Source} ({SourceMb:F0} MB -> {OutMb:F0} MB, saved {SavedMb:F0} MB){Vmaf}.",
            replaceResult.FinalPath, sourceSize / 1048576.0, replaceResult.OutputSize / 1048576.0,
            savedBytes / 1048576.0, job.Vmaf is null ? string.Empty : $", VMAF {job.Vmaf:F2}");
        Finish(job, JobState.Done, null);
    }

    private void Finish(TranscodeJob job, JobState state, string? error)
    {
        job.State = state;
        job.Error = error;
        job.CompletedUtc = DateTime.UtcNow;
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

    private void RecoverOrphanedBackups()
    {
        foreach (var job in _queue.Snapshot())
        {
            // Only an interrupted replace can leave a legitimately orphaned backup.
            // That state is persisted before the destructive step; Done jobs whose
            // source was later deleted by the user must not be touched.
            if (job.State != JobState.Replacing)
            {
                continue;
            }

            try
            {
                if (_replacer.RestoreOrphanedBackup(job.SourcePath))
                {
                    _logger.LogWarning(
                        "Recovered original from backup after an interrupted replace: {Source}",
                        job.SourcePath);
                }
                else if (!File.Exists(job.SourcePath + AtomicReplacer.BackupExtension))
                {
                    // No backup exists: either HardReplace (no crash safety by design) or
                    // the replace already finished. The idempotency check skips it if it is
                    // already the target codec; otherwise it is re-encoded.
                    _logger.LogWarning(
                        "Interrupted replace for {Source} left no backup; cannot auto-recover (HardReplace policy or already completed).",
                        job.SourcePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not restore backup for {Source}", job.SourcePath);
            }
        }
    }

    private void CleanupStaleTemps()
    {
        foreach (var job in _queue.Snapshot())
        {
            var directory = Path.GetDirectoryName(job.SourcePath);
            var fileName = Path.GetFileName(job.SourcePath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                foreach (var temp in Directory.EnumerateFiles(directory, fileName + ".tmp-*"))
                {
                    File.Delete(temp);
                    _logger.LogInformation("Removed stale temp file {Temp}.", temp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not clean temp files in {Directory}.", directory);
            }
        }
    }

    private void CleanupExpiredBackups(PluginConfiguration config)
    {
        if (config.ReplaceMode != ReplacePolicy.BackupThenDelete)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, config.BackupRetentionDays));
        foreach (var job in _queue.Snapshot())
        {
            if (job.State != JobState.Done || job.BackupPath is null || job.CompletedUtc is null)
            {
                continue;
            }

            if (job.CompletedUtc <= cutoff && _replacer.DeleteBackup(job.BackupPath))
            {
                _logger.LogInformation("Deleted expired backup {Backup}.", job.BackupPath);
                job.BackupPath = null;
                _queue.Update(job);
            }
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete temp file {Path}.", path);
        }
    }

    private static string SoftwarePreset() => Environment.ProcessorCount >= 16 ? "medium" : "slow";
}
