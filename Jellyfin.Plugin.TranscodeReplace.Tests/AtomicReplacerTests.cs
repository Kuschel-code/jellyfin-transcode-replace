using System;
using System.IO;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using Jellyfin.Plugin.TranscodeReplace.Replace;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

public class AtomicReplacerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tr-rep-" + Guid.NewGuid().ToString("N"));

    public AtomicReplacerTests() => Directory.CreateDirectory(_dir);

    // Fake runner. "ln" is emulated with File.Copy when it should succeed; on Windows
    // TryHardLink short-circuits and the replacer uses its own copy fallback. Either
    // path must produce the same observable result.
    private static FakeProcessRunner Runner(bool hardlinkSucceeds = true) => new((file, args) =>
    {
        if (file == "ln")
        {
            if (!hardlinkSucceeds)
            {
                return new ProcessResult(1, string.Empty, "no hardlink");
            }

            var parts = args.Split(' ');
            if (parts.Length == 2)
            {
                File.Copy(parts[0], parts[1], true);
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        }

        return new ProcessResult(0, string.Empty, string.Empty);
    });

    private static AtomicReplacer NewReplacer(IProcessRunner runner) =>
        new(new FilePermissions(runner), NullLogger<AtomicReplacer>.Instance);

    private (TranscodeJob Job, string Source, string Temp) Setup(string sourceContent = "OLD", string tempContent = "NEW")
    {
        var source = Path.Combine(_dir, "movie.mkv");
        var temp = Path.Combine(_dir, "movie.mkv.tmp-1.mkv");
        File.WriteAllText(source, sourceContent);
        File.WriteAllText(temp, tempContent);
        return (new TranscodeJob { SourcePath = source }, source, temp);
    }

    [Fact]
    public void BackupThenDelete_Replaces_And_Keeps_Backup()
    {
        var (job, source, temp) = Setup();
        var config = new PluginConfiguration { ReplaceMode = ReplacePolicy.BackupThenDelete };

        var result = NewReplacer(Runner()).Replace(job, temp, config);

        Assert.True(result.Success);
        Assert.Equal(source, result.FinalPath);
        Assert.Equal("NEW", File.ReadAllText(source));
        Assert.NotNull(result.BackupPath);
        Assert.Equal("OLD", File.ReadAllText(result.BackupPath!));
        Assert.False(File.Exists(temp));
    }

    [Fact]
    public void BackupThenDelete_Source_Always_Present_And_Original_Recoverable()
    {
        var (job, source, temp) = Setup();
        var config = new PluginConfiguration { ReplaceMode = ReplacePolicy.BackupThenDelete };

        // Copy-fallback path (hardlink reports failure).
        var result = NewReplacer(Runner(hardlinkSucceeds: false)).Replace(job, temp, config);

        Assert.True(result.Success);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(source + AtomicReplacer.BackupExtension));
        Assert.Equal("OLD", File.ReadAllText(source + AtomicReplacer.BackupExtension));
    }

    [Fact]
    public void BackupThenDelete_Fails_Cleanly_When_Temp_Missing()
    {
        var (job, source, _) = Setup();
        var missingTemp = Path.Combine(_dir, "does-not-exist.mkv");
        var config = new PluginConfiguration { ReplaceMode = ReplacePolicy.BackupThenDelete };

        var result = NewReplacer(Runner()).Replace(job, missingTemp, config);

        Assert.False(result.Success);
        Assert.Equal("OLD", File.ReadAllText(source));
        Assert.False(File.Exists(source + AtomicReplacer.BackupExtension));
    }

    [Fact]
    public void HardReplace_Overwrites_Without_Backup()
    {
        var (job, source, temp) = Setup();
        var config = new PluginConfiguration { ReplaceMode = ReplacePolicy.HardReplace };

        var result = NewReplacer(Runner()).Replace(job, temp, config);

        Assert.True(result.Success);
        Assert.Equal("NEW", File.ReadAllText(source));
        Assert.Null(result.BackupPath);
        Assert.False(File.Exists(source + AtomicReplacer.BackupExtension));
    }

    [Fact]
    public void SideBySide_Writes_Beside_And_Leaves_Original()
    {
        var (job, source, temp) = Setup();
        var config = new PluginConfiguration { ReplaceMode = ReplacePolicy.SideBySide };

        var result = NewReplacer(Runner()).Replace(job, temp, config);

        Assert.True(result.Success);
        Assert.Equal("OLD", File.ReadAllText(source));
        var beside = Path.Combine(_dir, "movie.transcoded.mkv");
        Assert.True(File.Exists(beside));
        Assert.Equal("NEW", File.ReadAllText(beside));
        Assert.Null(result.BackupPath);
        Assert.EndsWith(".transcoded.mkv", result.FinalPath);
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
