using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonJobQueue"/> class.
    /// </summary>
    /// <param name="path">Path to the JSON store.</param>
    public JsonJobQueue(string path)
    {
        _path = path;
        _jobs = Load(path);
    }

    /// <inheritdoc />
    public bool Enqueue(TranscodeJob job)
    {
        lock (_lock)
        {
            var duplicate = _jobs.Any(j =>
                string.Equals(j.SourcePath, job.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                j.SourceMtimeIso == job.SourceMtimeIso &&
                j.State != JobState.Failed);

            if (duplicate)
            {
                return false;
            }

            _jobs.Add(job);
            Save();
            return true;
        }
    }

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

    private static List<TranscodeJob> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new List<TranscodeJob>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<TranscodeJob>>(json, SerializerOptions)
                   ?? new List<TranscodeJob>();
        }
        catch (Exception)
        {
            // Corrupt store: start fresh rather than crash the plugin.
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
