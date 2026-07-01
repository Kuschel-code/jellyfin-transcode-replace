using System;
using System.IO;

namespace Jellyfin.Plugin.TranscodeReplace.Worker;

/// <summary>Free-space check before encoding a temp copy (plan section 3.9).</summary>
public static class DiskSpace
{
    /// <summary>
    /// Whether the drive holding <paramref name="path"/> has at least
    /// <paramref name="bytesNeeded"/> free. If the drive cannot be determined the
    /// check passes (it must not block on unusual mounts).
    /// </summary>
    /// <param name="path">A path on the target drive.</param>
    /// <param name="bytesNeeded">Bytes required.</param>
    /// <returns>True if there is enough room (or the check is inconclusive).</returns>
    public static bool HasRoomFor(string path, long bytesNeeded)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return true;
        }

        // On Unix Path.GetPathRoot is always "/", which reports the root filesystem
        // rather than the mount actually holding the media (e.g. /mnt/media). Query
        // the containing directory first — on Unix DriveInfo stats that path's own
        // filesystem — and only fall back to the path root (the Windows drive).
        foreach (var candidate in new[] { Path.GetDirectoryName(full), Path.GetPathRoot(full) })
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            try
            {
                return new DriveInfo(candidate).AvailableFreeSpace >= bytesNeeded;
            }
            catch (Exception)
            {
                // Not a valid drive name on this platform; try the next candidate.
            }
        }

        return true;
    }
}
