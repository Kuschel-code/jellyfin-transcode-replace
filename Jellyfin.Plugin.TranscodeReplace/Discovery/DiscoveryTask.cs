using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TranscodeReplace.Discovery;

/// <summary>
/// Scans libraries and enqueues transcode jobs. Does no encoding itself
/// (architecture plan section 3.0 / 3.3).
/// </summary>
public class DiscoveryTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IJobQueue _queue;
    private readonly ILogger<DiscoveryTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="queue">Job queue.</param>
    /// <param name="logger">Logger.</param>
    public DiscoveryTask(ILibraryManager libraryManager, IJobQueue queue, ILogger<DiscoveryTask> logger)
    {
        _libraryManager = libraryManager;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Transcode & Replace: Discover media";

    /// <inheritdoc />
    public string Key => "TranscodeReplaceDiscovery";

    /// <inheritdoc />
    public string Description => "Scans libraries and queues files for transcoding to the target codec.";

    /// <inheritdoc />
    public string Category => "Transcode & Replace";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        if (config.IncludedLibraryIds.Length > 0)
        {
            _logger.LogWarning(
                "IncludedLibraryIds is set but library scoping is not enforced yet; scanning all libraries.");
        }

        var query = new InternalItemsQuery
        {
            Recursive = true,
            IsVirtualItem = false,
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Video }
        };

        var items = _libraryManager.GetItemList(query);
        var total = items.Count;
        var processed = 0;
        var enqueued = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            progress.Report(total == 0 ? 100 : processed * 100.0 / total);

            if (TryBuildJob(item, config, out var job))
            {
                if (_queue.Enqueue(job))
                {
                    enqueued++;
                }
            }
        }

        _logger.LogInformation(
            "Discovery complete: {Enqueued} files queued out of {Total} scanned.", enqueued, total);
        progress.Report(100);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[]
    {
        new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
        }
    };

    private bool TryBuildJob(BaseItem item, PluginConfiguration config, out TranscodeJob job)
    {
        job = new TranscodeJob();

        var path = item.Path;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        if (!DiscoveryFilters.PassesPathFilters(path, config))
        {
            return false;
        }

        var videoStream = item.GetMediaStreams()?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
        var codec = videoStream?.Codec;
        var bitrateKbps = videoStream?.BitRate is { } b ? (int?)(b / 1000) : null;

        if (!DiscoveryFilters.QualifiesByCodec(codec, config) ||
            !DiscoveryFilters.QualifiesByBitrate(bitrateKbps, config))
        {
            return false;
        }

        long size;
        string mtime;
        try
        {
            var info = new FileInfo(path);
            size = info.Length;
            mtime = info.LastWriteTimeUtc.ToString("O");
        }
        catch (IOException)
        {
            return false;
        }

        job = new TranscodeJob
        {
            ItemId = item.Id,
            SourcePath = path,
            SourceSize = size,
            SourceMtimeIso = mtime,
            State = JobState.Pending,
            EnqueuedUtc = DateTime.UtcNow
        };
        return true;
    }
}
