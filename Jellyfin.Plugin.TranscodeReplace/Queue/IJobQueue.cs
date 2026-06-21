using System.Collections.Generic;

namespace Jellyfin.Plugin.TranscodeReplace.Queue;

/// <summary>
/// Persistent job queue contract.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Adds a job unless an equivalent non-failed job already exists for the same
    /// source path and modification time (idempotent enqueue).
    /// </summary>
    /// <param name="job">The job to add.</param>
    /// <returns>True if the job was added.</returns>
    bool Enqueue(TranscodeJob job);

    /// <summary>
    /// Atomically takes the next <see cref="JobState.Pending"/> job and marks it
    /// <see cref="JobState.Running"/>.
    /// </summary>
    /// <returns>The dequeued job, or null if none pending.</returns>
    TranscodeJob? Dequeue();

    /// <summary>Persists changes to an existing job.</summary>
    /// <param name="job">The job to update.</param>
    void Update(TranscodeJob job);

    /// <summary>Returns a snapshot copy of all jobs.</summary>
    /// <returns>All jobs.</returns>
    IReadOnlyList<TranscodeJob> Snapshot();

    /// <summary>
    /// Crash recovery: resets in-flight states (Running/Verifying/Replacing) back
    /// to Pending so they are retried after a restart.
    /// </summary>
    /// <returns>The number of jobs reset.</returns>
    int ResetInFlight();

    /// <summary>Whether an active (not finished) job exists for the given source path.</summary>
    /// <param name="sourcePath">Source file path.</param>
    /// <returns>True if an active job exists.</returns>
    bool HasActiveForSource(string sourcePath);
}
