using System;
using System.Linq;
using MediaBrowser.Controller.Session;

namespace Jellyfin.Plugin.TranscodeReplace.Worker;

/// <summary>
/// Inspects active playback sessions so the worker never replaces a file that is
/// being streamed and can pause while anything is playing (plan section 3.9).
/// </summary>
public sealed class PlaybackGuard
{
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackGuard"/> class.
    /// </summary>
    /// <param name="sessionManager">Session manager.</param>
    public PlaybackGuard(ISessionManager sessionManager) => _sessionManager = sessionManager;

    /// <summary>Whether a session is currently playing the given file.</summary>
    /// <param name="path">Source file path.</param>
    /// <returns>True if the file is in use.</returns>
    public bool IsFileInUse(string path) =>
        _sessionManager.Sessions.Any(s =>
            string.Equals(s.FullNowPlayingItem?.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether anything is currently playing.</summary>
    /// <returns>True if any session has a now-playing item.</returns>
    public bool IsAnythingPlaying() =>
        _sessionManager.Sessions.Any(s => s.NowPlayingItem is not null);
}
