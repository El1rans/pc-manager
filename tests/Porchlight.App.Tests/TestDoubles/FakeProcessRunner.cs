using Porchlight.Core.Processes;

namespace Porchlight.App.Tests.TestDoubles;

/// <summary>Shared <see cref="IProcessRunner"/> fake: every run succeeds with no output, and both
/// runs and detached starts are recorded so tests can assert on what a view model launched.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments)> RunCalls { get; } = [];

    public List<(string FileName, IReadOnlyList<string> Arguments)> StartDetachedCalls { get; } = [];

    public Task<ProcessRunResult> RunAsync(
        string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine, IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        RunCalls.Add((fileName, arguments));
        return Task.FromResult(new ProcessRunResult(0, [], []));
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
        StartDetachedCalls.Add((fileName, arguments));
}
