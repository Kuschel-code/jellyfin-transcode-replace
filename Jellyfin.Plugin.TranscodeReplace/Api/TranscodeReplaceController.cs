using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TranscodeReplace.Api;

/// <summary>Status snapshot returned to the configuration page.</summary>
public sealed class StatusDto
{
    /// <summary>Gets or sets the number of pending jobs.</summary>
    public int QueueLength { get; set; }

    /// <summary>Gets or sets the number of in-flight jobs.</summary>
    public int Running { get; set; }

    /// <summary>Gets or sets the number of completed jobs.</summary>
    public int Done { get; set; }

    /// <summary>Gets or sets the number of failed jobs.</summary>
    public int Failed { get; set; }

    /// <summary>Gets or sets the number of skipped jobs.</summary>
    public int Skipped { get; set; }

    /// <summary>Gets or sets the total bytes saved across completed jobs.</summary>
    public long SavedBytes { get; set; }

    /// <summary>Gets or sets the usable encoder names.</summary>
    public string[] UsableEncoders { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets a value indicating whether the plugin is in dry-run mode.</summary>
    public bool DryRun { get; set; }

    /// <summary>Gets or sets a short description of each job currently being processed.</summary>
    public string[] Active { get; set; } = Array.Empty<string>();
}

/// <summary>One row of the transcode history shown on the config page.</summary>
public sealed class JobDto
{
    /// <summary>Gets or sets the job id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin item id (for the poster image).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the item has a primary image.</summary>
    public bool HasPrimaryImage { get; set; }

    /// <summary>Gets or sets the job state.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Gets or sets the source path.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the source video codec.</summary>
    public string? SourceCodec { get; set; }

    /// <summary>Gets or sets the output video codec.</summary>
    public string? OutputCodec { get; set; }

    /// <summary>Gets or sets the source size in bytes.</summary>
    public long SourceSize { get; set; }

    /// <summary>Gets or sets the output size in bytes.</summary>
    public long? OutputSize { get; set; }

    /// <summary>Gets or sets the bytes saved.</summary>
    public long SavedBytes { get; set; }

    /// <summary>Gets or sets the VMAF score, if measured.</summary>
    public double? Vmaf { get; set; }

    /// <summary>Gets or sets the error/skip reason, if any.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the completion time (ISO-8601 UTC).</summary>
    public string? CompletedUtc { get; set; }
}

/// <summary>
/// Read-only endpoints for the configuration page (status and history).
/// Restricted to administrators.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("TranscodeReplace")]
[Produces("application/json")]
public class TranscodeReplaceController : ControllerBase
{
    private readonly IJobQueue _queue;
    private readonly HardwareProbe _probe;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscodeReplaceController"/> class.
    /// </summary>
    /// <param name="queue">Job queue.</param>
    /// <param name="probe">Hardware probe.</param>
    /// <param name="mediaEncoder">Media encoder.</param>
    /// <param name="libraryManager">Library manager (for item names and images).</param>
    public TranscodeReplaceController(
        IJobQueue queue,
        HardwareProbe probe,
        IMediaEncoder mediaEncoder,
        ILibraryManager libraryManager)
    {
        _queue = queue;
        _probe = probe;
        _mediaEncoder = mediaEncoder;
        _libraryManager = libraryManager;
    }

    /// <summary>Gets the current queue and encoder status.</summary>
    /// <returns>A status snapshot.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatusDto> GetStatus()
    {
        var jobs = _queue.Snapshot();
        var saved = jobs
            .Where(j => j.State == JobState.Done && j.OutputSize.HasValue)
            .Sum(j => Math.Max(0, j.SourceSize - j.OutputSize!.Value));

        var active = jobs
            .Where(j => j.State is JobState.Running or JobState.Verifying or JobState.Replacing)
            .Select(j => Path.GetFileName(j.SourcePath) + " (" + j.State + ")")
            .ToArray();

        return new StatusDto
        {
            QueueLength = jobs.Count(j => j.State == JobState.Pending),
            Running = active.Length,
            Done = jobs.Count(j => j.State == JobState.Done),
            Failed = jobs.Count(j => j.State == JobState.Failed),
            Skipped = jobs.Count(j => j.State == JobState.Skipped),
            SavedBytes = saved,
            UsableEncoders = UsableEncoderNames(),
            DryRun = Plugin.Instance?.Configuration.DryRun ?? true,
            Active = active
        };
    }

    /// <summary>Gets the transcode history, most recent first.</summary>
    /// <returns>The job history.</returns>
    [HttpGet("Jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<JobDto>> GetJobs()
    {
        var jobs = _queue.Snapshot()
            .OrderByDescending(j => j.CompletedUtc ?? j.EnqueuedUtc)
            .Take(500)
            .Select(ToDto)
            .ToList();

        return jobs;
    }

    private JobDto ToDto(TranscodeJob job)
    {
        var item = TryGetItem(job.ItemId);
        var saved = job.OutputSize.HasValue ? Math.Max(0, job.SourceSize - job.OutputSize.Value) : 0;

        return new JobDto
        {
            Id = job.Id.ToString("N"),
            ItemId = job.ItemId.ToString("N"),
            Name = item?.Name ?? Path.GetFileName(job.SourcePath),
            HasPrimaryImage = item?.HasImage(ImageType.Primary) ?? false,
            State = job.State.ToString(),
            SourcePath = job.SourcePath,
            SourceCodec = job.SourceCodec,
            OutputCodec = job.OutputCodec,
            SourceSize = job.SourceSize,
            OutputSize = job.OutputSize,
            SavedBytes = saved,
            Vmaf = job.Vmaf,
            Error = job.Error,
            CompletedUtc = job.CompletedUtc?.ToString("o")
        };
    }

    private BaseItem? TryGetItem(Guid id)
    {
        try
        {
            return id == Guid.Empty ? null : _libraryManager.GetItemById(id);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string[] UsableEncoderNames()
    {
        try
        {
            var path = _mediaEncoder.EncoderPath;
            return string.IsNullOrEmpty(path)
                ? Array.Empty<string>()
                : _probe.Usable(path).Select(e => e.Name).ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
