using System;
using System.Linq;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using MediaBrowser.Controller.MediaEncoding;
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
}

/// <summary>
/// Read-only status endpoint for the configuration page. Restricted to
/// administrators because it exposes encoder details and queue counts.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscodeReplaceController"/> class.
    /// </summary>
    /// <param name="queue">Job queue.</param>
    /// <param name="probe">Hardware probe.</param>
    /// <param name="mediaEncoder">Media encoder.</param>
    public TranscodeReplaceController(IJobQueue queue, HardwareProbe probe, IMediaEncoder mediaEncoder)
    {
        _queue = queue;
        _probe = probe;
        _mediaEncoder = mediaEncoder;
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

        return new StatusDto
        {
            QueueLength = jobs.Count(j => j.State == JobState.Pending),
            Running = jobs.Count(j => j.State is JobState.Running or JobState.Verifying or JobState.Replacing),
            Done = jobs.Count(j => j.State == JobState.Done),
            Failed = jobs.Count(j => j.State == JobState.Failed),
            Skipped = jobs.Count(j => j.State == JobState.Skipped),
            SavedBytes = saved,
            UsableEncoders = UsableEncoderNames()
        };
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
