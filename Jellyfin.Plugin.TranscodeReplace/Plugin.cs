using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.TranscodeReplace.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TranscodeReplace;

/// <summary>
/// The Transcode &amp; Replace plugin entry point.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Server application paths.</param>
    /// <param name="xmlSerializer">XML serializer for the configuration.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the singleton plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Transcode & Replace";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("eff73e61-0320-4bce-bb2c-27fb68604e9d");

    /// <inheritdoc />
    public override string Description =>
        "Batch-transcodes library media to a target codec and atomically replaces the original file, " +
        "with hardware detection, verification gates, HDR preservation and a persistent job queue.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = "transcodereplace",
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace)
        }
    };
}
