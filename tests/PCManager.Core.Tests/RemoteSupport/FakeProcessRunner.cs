using PCManager.Core.Processes;

namespace PCManager.Core.Tests.RemoteSupport;

/// <summary>
/// <see cref="IProcessRunner"/> fake that maps each CLI argument (e.g. <c>--get-id</c>) to a
/// canned result, so <c>AnyDeskService</c> tests can give <c>--get-id</c> and <c>--get-alias</c>
/// different answers in the same run - unlike the single-shared-result fake in
/// <c>PCManager.Core.Tests.Components</c>.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessRunResult> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<ProcessRunResult>> _resultSequences = new(StringComparer.Ordinal);

    public List<string> RunArguments { get; } = [];

    public void SetResult(string argument, int exitCode, string? line = null) =>
        _results[argument] = new ProcessRunResult(exitCode, line is null ? [] : [line], []);

    /// <summary>Queues one-shot results for repeated calls to the same <paramref name="argument"/>
    /// (e.g. <c>--get-id</c> failing on the first poll, then succeeding once AnyDesk has started) -
    /// each call dequeues the next one; once empty, falls back to <see cref="SetResult"/>.</summary>
    public void QueueResults(string argument, params (int ExitCode, string? Line)[] results)
    {
        if (!_resultSequences.TryGetValue(argument, out var queue))
        {
            queue = new Queue<ProcessRunResult>();
            _resultSequences[argument] = queue;
        }

        foreach (var (exitCode, line) in results)
        {
            queue.Enqueue(new ProcessRunResult(exitCode, line is null ? [] : [line], []));
        }
    }

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        var argument = arguments[0];
        RunArguments.Add(argument);

        if (_resultSequences.TryGetValue(argument, out var queue) && queue.Count > 0)
        {
            return Task.FromResult(queue.Dequeue());
        }

        var result = _results.TryGetValue(argument, out var configured)
            ? configured
            : new ProcessRunResult(1, [], []);
        return Task.FromResult(result);
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments)
    {
    }
}
