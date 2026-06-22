using System;
using System.IO;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Replace;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class AtomicReplacerRecoveryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tr-rec-" + Guid.NewGuid().ToString("N"));

    public AtomicReplacerRecoveryTests() => Directory.CreateDirectory(_dir);

    private static AtomicReplacer NewReplacer()
    {
        var runner = new FakeProcessRunner((_, _) => new ProcessResult(0, string.Empty, string.Empty));
        return new AtomicReplacer(new FilePermissions(runner), NullLogger<AtomicReplacer>.Instance);
    }

    [Fact]
    public void RestoreOrphanedBackup_Restores_When_Source_Missing_And_Backup_Present()
    {
        var source = Path.Combine(_dir, "movie.mkv");
        File.WriteAllText(source + AtomicReplacer.BackupExtension, "original");

        var restored = NewReplacer().RestoreOrphanedBackup(source);

        Assert.True(restored);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(source + AtomicReplacer.BackupExtension));
        Assert.Equal("original", File.ReadAllText(source));
    }

    [Fact]
    public void RestoreOrphanedBackup_NoOp_When_Source_Present()
    {
        var source = Path.Combine(_dir, "movie.mkv");
        File.WriteAllText(source, "current");
        File.WriteAllText(source + AtomicReplacer.BackupExtension, "old");

        var restored = NewReplacer().RestoreOrphanedBackup(source);

        Assert.False(restored);
        Assert.Equal("current", File.ReadAllText(source));
        Assert.True(File.Exists(source + AtomicReplacer.BackupExtension));
    }

    [Fact]
    public void RestoreOrphanedBackup_NoOp_When_Backup_Missing()
    {
        var source = Path.Combine(_dir, "movie.mkv");

        var restored = NewReplacer().RestoreOrphanedBackup(source);

        Assert.False(restored);
        Assert.False(File.Exists(source));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
