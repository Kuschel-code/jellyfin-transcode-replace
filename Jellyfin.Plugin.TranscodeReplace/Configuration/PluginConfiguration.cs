using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TranscodeReplace.Configuration;

/// <summary>Target video codec for the re-encode.</summary>
public enum TargetVideoCodec
{
    /// <summary>H.264 / AVC.</summary>
    H264,

    /// <summary>HEVC / H.265.</summary>
    Hevc,

    /// <summary>AV1.</summary>
    Av1
}

/// <summary>How the encoder is chosen.</summary>
public enum EncoderPreferenceMode
{
    /// <summary>Probe and pick automatically.</summary>
    Auto,

    /// <summary>Always use a software encoder.</summary>
    ForceSoftware,

    /// <summary>Prefer a hardware encoder.</summary>
    ForceHardware,

    /// <summary>Use the encoder named in <see cref="PluginConfiguration.SpecificEncoder"/>.</summary>
    Specific
}

/// <summary>Quality vs speed trade-off when ranking encoders.</summary>
public enum QualityVsSpeed
{
    /// <summary>Prefer software encoders for best quality-per-bit.</summary>
    Quality,

    /// <summary>Prefer hardware encoders for speed.</summary>
    Speed
}

/// <summary>How audio streams are handled.</summary>
public enum AudioHandling
{
    /// <summary>Copy audio without re-encoding.</summary>
    Copy,

    /// <summary>Copy original and add a compatibility AAC track.</summary>
    AddAac,

    /// <summary>Transcode audio to AAC.</summary>
    Transcode
}

/// <summary>Preferred output container.</summary>
public enum ContainerPreference
{
    /// <summary>Keep the source container.</summary>
    Keep,

    /// <summary>Matroska.</summary>
    Mkv,

    /// <summary>MP4.</summary>
    Mp4
}

/// <summary>How the original file is replaced.</summary>
public enum ReplacePolicy
{
    /// <summary>Move original to a backup, delete after the retention window.</summary>
    BackupThenDelete,

    /// <summary>Keep the original next to the new file, never delete.</summary>
    SideBySide,

    /// <summary>Overwrite the original immediately (opt-in, no backup).</summary>
    HardReplace
}

/// <summary>
/// Plugin configuration. See the architecture plan section 4.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the target video codec.</summary>
    public TargetVideoCodec TargetVideoCodec { get; set; } = TargetVideoCodec.Hevc;

    /// <summary>Gets or sets the CRF/CQ quality value.</summary>
    public int QualityValue { get; set; } = 23;

    /// <summary>Gets or sets the encoder selection mode.</summary>
    public EncoderPreferenceMode EncoderPreference { get; set; } = EncoderPreferenceMode.Auto;

    /// <summary>Gets or sets the explicit encoder name when <see cref="EncoderPreference"/> is Specific.</summary>
    public string? SpecificEncoder { get; set; }

    /// <summary>Gets or sets the quality-vs-speed bias.</summary>
    public QualityVsSpeed Mode { get; set; } = QualityVsSpeed.Quality;

    /// <summary>Gets or sets the maximum number of concurrent encode jobs.</summary>
    public int MaxParallelJobs { get; set; } = 1;

    /// <summary>Gets or sets the audio handling mode.</summary>
    public AudioHandling AudioMode { get; set; } = AudioHandling.Copy;

    /// <summary>Gets or sets the preferred output container.</summary>
    public ContainerPreference PreferredContainer { get; set; } = ContainerPreference.Keep;

    /// <summary>Gets or sets the library IDs to include (empty = all).</summary>
    public string[] IncludedLibraryIds { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets path include globs.</summary>
    public string[] PathIncludeGlobs { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets path exclude globs.</summary>
    public string[] PathExcludeGlobs { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the minimum source bitrate (kbps) to qualify a file.</summary>
    public int MinSourceBitrateKbps { get; set; }

    /// <summary>Gets or sets a value indicating whether to skip files already in the target codec.</summary>
    public bool SkipIfAlreadyTargetCodec { get; set; } = true;

    /// <summary>Gets or sets the replace policy.</summary>
    public ReplacePolicy ReplaceMode { get; set; } = ReplacePolicy.BackupThenDelete;

    /// <summary>Gets or sets the backup retention in days.</summary>
    public int BackupRetentionDays { get; set; } = 7;

    /// <summary>Gets or sets a value indicating whether the VMAF gate is enabled.</summary>
    public bool EnableVmafGate { get; set; }

    /// <summary>Gets or sets the VMAF score threshold.</summary>
    public double VmafThreshold { get; set; } = 95;

    /// <summary>Gets or sets a value indicating whether HDR sources are skipped.</summary>
    public bool SkipHdr { get; set; }

    /// <summary>Gets or sets a value indicating whether Dolby Vision sources are skipped.</summary>
    public bool SkipDolbyVision { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to run only while the server is idle.</summary>
    public bool RunOnlyWhenIdle { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to simulate without writing (dry-run).</summary>
    public bool DryRun { get; set; } = true;
}
