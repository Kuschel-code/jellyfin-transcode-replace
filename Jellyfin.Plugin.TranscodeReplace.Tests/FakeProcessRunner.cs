using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranscodeReplace.Hardware;

namespace Jellyfin.Plugin.TranscodeReplace.Tests;

/// <summary>Test double for <see cref="IProcessRunner"/> returning canned results.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Func<string, string, ProcessResult> _handler;

    public FakeProcessRunner(Func<string, string, ProcessResult> handler) => _handler = handler;

    public List<(string File, string Args)> Calls { get; } = new();

    public ProcessResult Run(string fileName, string arguments, int timeoutMs = 30000)
    {
        Calls.Add((fileName, arguments));
        return _handler(fileName, arguments);
    }

    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add((fileName, arguments));
        return Task.FromResult(_handler(fileName, arguments));
    }
}
