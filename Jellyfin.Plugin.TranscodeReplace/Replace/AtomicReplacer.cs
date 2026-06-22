using System;
using System.IO;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TranscodeReplace.Replace;

/// <summary>The result of a replace operation.</summary>
/// <param name="Success">Whether the replace succeeded.</param>
/// <param name="FinalPath">The path the output ended up at.</param>
/// <param name="OutputSize">Output size in bytes.</param>
/// <param name="BackupPath">Backup path retained, if any.</param>
/// <param name="Error">Error message on failure.</param>
public sealed record ReplaceResult(bool Success, string FinalPath, long OutputSize, string? BackupPath, string? Error);

/// <summary>
/// Replaces the original file with the verified output, preserving permissions and
/// (for the default policy) keeping a backup until verified (plan section 3.8). The
/// temp file is expected to live in the same directory as the source, so the rename
/// is atomic on a single filesystem.
/// </summary>
public sealed class AtomicReplacer
{
    /// <summary>File extension used for the retained backup of the original.</summary>
    public const string BackupExtension = ".trbak";

    private readonly FilePermissions _permissions;
    private readonly ILogger<AtomicReplacer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicReplacer"/> class.
    /// </summary>
    /// <param name="permissions">Permission helper.</param>
    /// <param name="logger">Logger.</param>
    public AtomicReplacer(FilePermissions permissions, ILogger<AtomicReplacer> logger)
    {
        _permissions = permissions;
        _logger = logger;
    }

    /// <summary>Performs the replace according to the configured policy.</summary>
    /// <param name="job">The job.</param>
    /// <param name="tempPath">The verified temp output path.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>The replace result.</returns>
    public ReplaceResult Replace(TranscodeJob job, string tempPath, PluginConfiguration config)
    {
        var source = job.SourcePath;

        if (!File.Exists(tempPath))
        {
            return new ReplaceResult(false, source, 0, null, "temp output is missing");
        }

        var outputSize = new FileInfo(tempPath).Length;

        try
        {
            switch (config.ReplaceMode)
            {
                case ReplacePolicy.SideBySide:
                    return ReplaceSideBySide(source, tempPath, outputSize);

                case ReplacePolicy.HardReplace:
                    return ReplaceHard(source, tempPath, outputSize);

                case ReplacePolicy.BackupThenDelete:
                default:
                    return ReplaceWithBackup(source, tempPath, outputSize);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replace failed for {Source}", source);
            TryDelete(tempPath);
            return new ReplaceResult(false, source, outputSize, null, ex.Message);
        }
    }

    /// <summary>
    /// Crash recovery: if the source path is missing but its backup exists, move the
    /// backup back to the source path. Keyed on the filesystem state (not job state),
    /// because the backup path is only persisted after a successful replace.
    /// </summary>
    /// <param name="sourcePath">The original source path.</param>
    /// <returns>True if the original was restored from a backup.</returns>
    public bool RestoreOrphanedBackup(string sourcePath)
    {
        var backup = sourcePath + BackupExtension;
        if (File.Exists(sourcePath) || !File.Exists(backup))
        {
            return false;
        }

        File.Move(backup, sourcePath);
        return true;
    }

    /// <summary>Deletes a retained backup file. Best effort.</summary>
    /// <param name="backupPath">Backup path.</param>
    /// <returns>True if the backup no longer exists afterwards.</returns>
    public bool DeleteBackup(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete backup {Backup}", backupPath);
            return false;
        }
    }

    private ReplaceResult ReplaceSideBySide(string source, string tempPath, long outputSize)
    {
        var directory = Path.GetDirectoryName(source) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(source);
        var ext = Path.GetExtension(tempPath);
        var target = Path.Combine(directory, baseName + ".transcoded" + ext);

        var perms = _permissions.Snapshot(source);
        File.Move(tempPath, target, overwrite: true);
        _permissions.Apply(target, perms);
        _logger.LogInformation("Wrote transcoded copy beside original: {Target}", target);
        return new ReplaceResult(true, target, outputSize, null, null);
    }

    private ReplaceResult ReplaceHard(string source, string tempPath, long outputSize)
    {
        var perms = _permissions.Snapshot(source);
        File.Move(tempPath, source, overwrite: true);
        _permissions.Apply(source, perms);
        Touch(source);
        return new ReplaceResult(true, source, outputSize, null, null);
    }

    private ReplaceResult ReplaceWithBackup(string source, string tempPath, long outputSize)
    {
        var backup = source + BackupExtension;
        var perms = _permissions.Snapshot(source);

        if (File.Exists(backup))
        {
            File.Delete(backup);
        }

        // Back up the original without removing it from its path: a hardlink when the
        // filesystem supports it (no data copied), otherwise a full copy. The source
        // stays in place throughout.
        if (!_permissions.TryHardLink(source, backup))
        {
            File.Copy(source, backup, overwrite: true);
        }

        try
        {
            // One atomic rename over the source (temp is co-located, so this is a
            // rename(2)). The old inode survives through the backup link/copy, so the
            // source path always resolves to either the old or the new file, never to
            // nothing.
            File.Move(tempPath, source, overwrite: true);
        }
        catch
        {
            // The destructive step never began: the source is still the original.
            // Drop the backup we just created so we don't leave a stray .trbak.
            TryDelete(backup);
            throw;
        }

        _permissions.Apply(source, perms);
        Touch(source);
        return new ReplaceResult(true, source, outputSize, backup, null);
    }

    private static void Touch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception)
        {
            // Non-fatal: a later scan still detects the size change.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Best effort.
        }
    }
}
