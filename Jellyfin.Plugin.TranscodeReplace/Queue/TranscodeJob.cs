using System;

namespace Jellyfin.Plugin.TranscodeReplace.Queue;

/// <summary>
/// A single transcode job. Persisted to <c>jobs.json</c>.
/// </summary>
public sealed class TranscodeJob
{
    /// <summary>Gets or sets the unique job id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the Jellyfin item id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the source file path.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the source size in bytes at enqueue time.</summary>
    public long SourceSize { get; set; }

    /// <summary>Gets or sets the source last-write time as an ISO-8601 string (idempotency key).</summary>
    public string SourceMtimeIso { get; set; } = string.Empty;

    /// <summary>Gets or sets the current state.</summary>
    public JobState State { get; set; } = JobState.Pending;

    /// <summary>Gets or sets the last error message, if any.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the number of attempts made.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets the measured VMAF score, if computed.</summary>
    public double? Vmaf { get; set; }

    /// <summary>Gets or sets the size of the produced output in bytes, if known.</summary>
    public long? OutputSize { get; set; }

    /// <summary>Gets or sets the source video codec (from ffprobe).</summary>
    public string? SourceCodec { get; set; }

    /// <summary>Gets or sets the output video codec (from ffprobe), if produced.</summary>
    public string? OutputCodec { get; set; }

    /// <summary>Gets or sets the backup path retained until the retention window expires.</summary>
    public string? BackupPath { get; set; }

    /// <summary>Gets or sets the UTC enqueue time.</summary>
    public DateTime EnqueuedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC completion time (Done/Failed/Skipped).</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>Creates a shallow copy.</summary>
    /// <returns>A copy of this job.</returns>
    public TranscodeJob Clone() => (TranscodeJob)MemberwiseClone();
}
