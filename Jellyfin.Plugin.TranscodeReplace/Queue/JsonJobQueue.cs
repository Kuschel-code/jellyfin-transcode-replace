using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.TranscodeReplace.Verify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.TranscodeReplace.Queue;

/// <summary>
/// File-backed <see cref="IJobQueue"/>. Single JSON file, in-process lock,
/// atomic writes. No external dependency (deliberately not LiteDB, to avoid
/// shipping transitive native assemblies with the plugin).
/// </summary>
public sealed class JsonJobQueue : IJobQueue
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<TranscodeJob> _jobs;
    private readonly ILogger<JsonJobQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonJobQueue"/> class.
    /// </summary>
    /// <param name="path">Path to the JSON store.</param>
    /// <param name="logger">Logger (optional; a null logger is used when omitted).</param>
    public JsonJobQueue(string path, ILogger<JsonJobQueue>? logger = null)
    {
        _path = path;
        _logger = logger ?? NullLogger<JsonJobQueue>.Instance;
        _jobs = Load();
    }

    /// <inheritdoc />
    public bool Enqueue(TranscodeJob job)
    {
        lock (_lock)
        {
            var existing = _jobs.Where(j =>
                string.Equals(j.SourcePath, job.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                j.SourceMtimeIso == job.SourceMtimeIso).ToList();

            // A duplicate blocks unless the previous verdict is retryable. Failures and
            // skips are retryable: skip verdicts depend on the configuration (dry-run,
            // target codec, HDR policy, encoder availability), so they must be
            // re-evaluated on the next discovery run — the re-check costs one ffprobe.
            // The exceptions are active/Done jobs and the not-smaller verdict, which
            // cost a full encode for this exact file version and must not repeat.
            if (existing.Any(j => !IsRetryable(j)))
            {
                return false;
            }

            // Replace the superseded retryable entries instead of accumulating one
            // stale row per discovery run.
            foreach (var stale in existing)
            {
                _jobs.Remove(stale);
            }

            _jobs.Add(job);
            Save();
            return true;
        }
    }

    private static bool IsRetryable(TranscodeJob job) =>
        job.State == JobState.Failed ||
        (job.State == JobState.Skipped &&
         !string.Equals(job.Error, Verifier.NotSmallerReason, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public TranscodeJob? Dequeue()
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.State == JobState.Pending);
            if (job is null)
            {
                return null;
            }

            job.State = JobState.Running;
            Save();
            return job.Clone();
        }
    }

    /// <inheritdoc />
    public void Update(TranscodeJob job)
    {
        lock (_lock)
        {
            var index = _jobs.FindIndex(j => j.Id == job.Id);
            if (index >= 0)
            {
                _jobs[index] = job;
            }
            else
            {
                _jobs.Add(job);
            }

            Save();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TranscodeJob> Snapshot()
    {
        lock (_lock)
        {
            return _jobs.Select(j => j.Clone()).ToList();
        }
    }

    /// <inheritdoc />
    public int ResetInFlight()
    {
        lock (_lock)
        {
            var count = 0;
            foreach (var job in _jobs)
            {
                if (job.State is JobState.Running or JobState.Verifying or JobState.Replacing)
                {
                    job.State = JobState.Pending;
                    count++;
                }
            }

            if (count > 0)
            {
                Save();
            }

            return count;
        }
    }

    /// <inheritdoc />
    public bool HasActiveForSource(string sourcePath)
    {
        lock (_lock)
        {
            return _jobs.Any(j =>
                string.Equals(j.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) &&
                j.State is JobState.Pending or JobState.Running or JobState.Verifying or JobState.Replacing);
        }
    }

    private List<TranscodeJob> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new List<TranscodeJob>();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<TranscodeJob>>(json, SerializerOptions)
                   ?? new List<TranscodeJob>();
        }
        catch (Exception ex)
        {
            // Corrupt store: start fresh rather than crash the plugin, but make the
            // loss visible instead of dropping the queue silently.
            _logger.LogError(ex, "Job queue at {Path} is corrupt or unreadable; starting with an empty queue. Pending jobs are lost.", _path);
            return new List<TranscodeJob>();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_jobs, SerializerOptions));

        if (File.Exists(_path))
        {
            File.Replace(tmp, _path, null);
        }
        else
        {
            File.Move(tmp, _path);
        }
    }
}
