namespace Jellyfin.Plugin.TranscodeReplace.Queue;

/// <summary>
/// Lifecycle state of a transcode job (architecture plan section 3.3).
/// </summary>
public enum JobState
{
    /// <summary>Waiting to be picked up.</summary>
    Pending,

    /// <summary>ffmpeg is encoding.</summary>
    Running,

    /// <summary>Output is being verified (ffprobe / VMAF gates).</summary>
    Verifying,

    /// <summary>Original is being atomically replaced.</summary>
    Replacing,

    /// <summary>Completed successfully.</summary>
    Done,

    /// <summary>Failed (a gate failed or ffmpeg errored). Original untouched.</summary>
    Failed,

    /// <summary>Skipped (already target codec, in use, filtered).</summary>
    Skipped
}
