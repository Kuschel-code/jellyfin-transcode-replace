using System.IO;
using Jellyfin.Plugin.TranscodeReplace.Encoding;
using Jellyfin.Plugin.TranscodeReplace.Hardware;
using Jellyfin.Plugin.TranscodeReplace.Queue;
using Jellyfin.Plugin.TranscodeReplace.Replace;
using Jellyfin.Plugin.TranscodeReplace.Verify;
using Jellyfin.Plugin.TranscodeReplace.Worker;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TranscodeReplace;

/// <summary>
/// Registers plugin services with the host DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IProcessRunner, ProcessRunner>();
        serviceCollection.AddSingleton<HardwareProbe>();
        serviceCollection.AddSingleton<ArgBuilder>();
        serviceCollection.AddSingleton<FfmpegRunner>();
        serviceCollection.AddSingleton<MediaInfoProbe>();
        serviceCollection.AddSingleton<VmafGate>();
        serviceCollection.AddSingleton<FilePermissions>();
        serviceCollection.AddSingleton<AtomicReplacer>();
        serviceCollection.AddSingleton<PlaybackGuard>();

        // Persistent queue lives in the plugin data folder so it survives restarts.
        serviceCollection.AddSingleton<IJobQueue>(sp =>
        {
            var dataDir = Plugin.Instance!.DataFolderPath;
            Directory.CreateDirectory(dataDir);
            return new JsonJobQueue(
                Path.Combine(dataDir, "jobs.json"),
                sp.GetRequiredService<ILogger<JsonJobQueue>>());
        });

        serviceCollection.AddHostedService<TranscodeWorker>();
    }
}
