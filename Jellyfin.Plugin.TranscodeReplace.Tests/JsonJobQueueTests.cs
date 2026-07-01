using System;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using Jellyfin.Plugin.TranscodeReplace.Verify;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class JsonJobQueueTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "tr-tests-" + Guid.NewGuid() + ".json");

    [Fact]
    public void Enqueue_Is_Idempotent_On_Same_Source_And_Mtime()
    {
        var queue = new JsonJobQueue(_path);

        Assert.True(queue.Enqueue(NewJob()));
        Assert.False(queue.Enqueue(NewJob())); // duplicate
        Assert.Single(queue.Snapshot());
    }

    [Fact]
    public void Dequeue_Marks_Running_And_Persists()
    {
        var queue = new JsonJobQueue(_path);
        queue.Enqueue(NewJob());

        var job = queue.Dequeue();
        Assert.NotNull(job);
        Assert.Equal(JobState.Running, queue.Snapshot().Single().State);

        // Reload from disk: state survived the process.
        var reloaded = new JsonJobQueue(_path);
        Assert.Equal(JobState.Running, reloaded.Snapshot().Single().State);
    }

    [Fact]
    public void ResetInFlight_Returns_Running_To_Pending()
    {
        var queue = new JsonJobQueue(_path);
        queue.Enqueue(NewJob());
        queue.Dequeue();

        var reloaded = new JsonJobQueue(_path);
        Assert.Equal(1, reloaded.ResetInFlight());
        Assert.Equal(JobState.Pending, reloaded.Snapshot().Single().State);
    }

    [Fact]
    public void HasActiveForSource_Tracks_Active_Jobs()
    {
        var queue = new JsonJobQueue(_path);
        queue.Enqueue(NewJob());
        Assert.True(queue.HasActiveForSource("/media/a.mkv"));
        Assert.False(queue.HasActiveForSource("/media/other.mkv"));
    }

    [Fact]
    public void Enqueue_Retries_After_DryRun_Skip_And_Replaces_The_Stale_Entry()
    {
        var queue = new JsonJobQueue(_path);
        var dryRun = NewJob();
        dryRun.State = JobState.Skipped;
        dryRun.Error = "dry-run (no file written)";
        queue.Update(dryRun);

        Assert.True(queue.Enqueue(NewJob()));
        var jobs = queue.Snapshot();
        Assert.Single(jobs);
        Assert.Equal(JobState.Pending, jobs[0].State);
    }

    [Fact]
    public void Enqueue_Retries_After_Config_Dependent_Skip()
    {
        var queue = new JsonJobQueue(_path);
        var skipped = NewJob();
        skipped.State = JobState.Skipped;
        skipped.Error = "no usable encoder for target codec Av1";
        queue.Update(skipped);

        Assert.True(queue.Enqueue(NewJob()));
        Assert.Single(queue.Snapshot());
    }

    [Fact]
    public void Enqueue_Retries_After_Failure_And_Replaces_The_Stale_Entry()
    {
        var queue = new JsonJobQueue(_path);
        var failed = NewJob();
        failed.State = JobState.Failed;
        failed.Error = "ffmpeg exited with code 1";
        queue.Update(failed);

        Assert.True(queue.Enqueue(NewJob()));
        Assert.Single(queue.Snapshot());
    }

    [Fact]
    public void Enqueue_Blocks_After_NotSmaller_Skip()
    {
        var queue = new JsonJobQueue(_path);
        var notSmaller = NewJob();
        notSmaller.State = JobState.Skipped;
        notSmaller.Error = Verifier.NotSmallerReason;
        queue.Update(notSmaller);

        Assert.False(queue.Enqueue(NewJob()));
        Assert.Single(queue.Snapshot());
    }

    [Fact]
    public void Enqueue_Blocks_While_Done_For_Same_File_Version()
    {
        var queue = new JsonJobQueue(_path);
        var done = NewJob();
        done.State = JobState.Done;
        queue.Update(done);

        Assert.False(queue.Enqueue(NewJob()));
    }

    private static TranscodeJob NewJob() => new()
    {
        SourcePath = "/media/a.mkv",
        SourceMtimeIso = "2026-06-21T00:00:00.0000000Z",
        State = JobState.Pending
    };

    public void Dispose()
    {
        foreach (var p in new[] { _path, _path + ".tmp" })
        {
            if (File.Exists(p))
            {
                File.Delete(p);
            }
        }
    }
}
