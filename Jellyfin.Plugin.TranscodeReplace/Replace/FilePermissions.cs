using System;
using System.IO;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Replace;

/// <summary>A snapshot of a file's Unix permissions and owner.</summary>
/// <param name="Mode">The Unix file mode bits.</param>
/// <param name="Owner">Owner as <c>uid:gid</c>, or null if unknown.</param>
public sealed record PermSnapshot(UnixFileMode Mode, string? Owner);

/// <summary>
/// Captures and re-applies POSIX permissions/owner across a replace, so a replaced
/// file keeps the original's mode and owner (important on TrueNAS/SMB shares — plan
/// section 3.8). No-op on non-Unix platforms.
/// </summary>
public sealed class FilePermissions
{
    private readonly IProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePermissions"/> class.
    /// </summary>
    /// <param name="runner">Process runner (for stat/chown).</param>
    public FilePermissions(IProcessRunner runner) => _runner = runner;

    /// <summary>Reads the mode and owner of a file. Returns null off Unix or on error.</summary>
    /// <param name="path">File path.</param>
    /// <returns>The snapshot, or null.</returns>
    public PermSnapshot? Snapshot(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return null;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            var result = _runner.Run("stat", new[] { "-c", "%u:%g", path }, 5000);
            var owner = result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
            return new PermSnapshot(mode, string.IsNullOrWhiteSpace(owner) ? null : owner);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Applies a previously captured snapshot to a file. Best effort.</summary>
    /// <param name="path">File path.</param>
    /// <param name="snapshot">Snapshot to apply (null is a no-op).</param>
    public void Apply(string path, PermSnapshot? snapshot)
    {
        if (snapshot is null || (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, snapshot.Mode);
        }
        catch (Exception)
        {
            // Best effort.
        }

        if (!string.IsNullOrEmpty(snapshot.Owner))
        {
            try
            {
                _runner.Run("chown", new[] { snapshot.Owner, path }, 5000);
            }
            catch (Exception)
            {
                // Best effort.
            }
        }
    }

    /// <summary>
    /// Tries to create a hardlink. Returns false when the platform or filesystem
    /// cannot do it (Windows, SMB, cross-device), so the caller can fall back to a
    /// copy.
    /// </summary>
    /// <param name="source">Existing file.</param>
    /// <param name="link">New link path.</param>
    /// <returns>True if the link was created.</returns>
    public bool TryHardLink(string source, string link)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            var result = _runner.Run("ln", new[] { source, link }, 5000);
            return result.ExitCode == 0 && File.Exists(link);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
