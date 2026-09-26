using PCManager.Core.Processes;

namespace PCManager.Core.Tests.Components;

internal sealed class FakeProcessRunner : IProcessRunner
{
    public int NextExitCode { get; set; }

    public IReadOnlyList<string> NextLines { get; set; } = [];

    /// <summary>Every install/run invocation, so tests can assert on the exact winget arguments used.</summary>
    public List<(string FileName, IReadOnlyList<string> Arguments)> RunCalls { get; } = [];

    public List<(string FileName, IReadOnlyList<string> Arguments)> StartDetachedCalls { get; } = [];

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        RunCalls.Add((fileName, arguments));
        return Task.FromResult(new ProcessRunResult(NextExitCode, NextLines));
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
        StartDetachedCalls.Add((fileName, arguments));
}
