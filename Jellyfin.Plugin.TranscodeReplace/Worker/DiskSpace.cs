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
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return true;
            }

            return new DriveInfo(root).AvailableFreeSpace >= bytesNeeded;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
