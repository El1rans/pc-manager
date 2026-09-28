using Porchlight.Core.Processes;

namespace Porchlight.Core.Tests.Components;

internal sealed class FakeProcessRunner : IProcessRunner
{
    public int NextExitCode { get; set; }

    public IReadOnlyList<string> NextLines { get; set; } = [];

    public IReadOnlyList<string> NextErrorLines { get; set; } = [];

    /// <summary>Records the cancellation token each <see cref="RunAsync"/> call actually received,
    /// so tests can assert that an install passes <see cref="CancellationToken.None"/> once
    /// launched (see <c>ComponentService.InstallAsync</c>).</summary>
    public List<CancellationToken> RunCancellationTokens { get; } = [];

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
        RunCancellationTokens.Add(cancellationToken);
        return Task.FromResult(new ProcessRunResult(NextExitCode, NextLines, NextErrorLines));
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
        StartDetachedCalls.Add((fileName, arguments));
}
