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
    private readonly HashSet<string> _hangingArguments = new(StringComparer.Ordinal);

    public List<string> RunArguments { get; } = [];

    public void SetResult(string argument, int exitCode, string? line = null) =>
        _results[argument] = new ProcessRunResult(exitCode, line is null ? [] : [line], []);

    public void SetLines(string argument, int exitCode, IReadOnlyList<string> lines) =>
        _results[argument] = new ProcessRunResult(exitCode, lines, []);

    /// <summary>Makes <see cref="RunAsync"/> for <paramref name="argument"/> never complete on its
    /// own, so the caller's own timeout (a linked <see cref="CancellationToken"/>) is what ends it -
    /// used to test <c>AnyDeskService</c>'s "the CLI call itself timed out" handling.</summary>
    public void Hang(string argument) => _hangingArguments.Add(argument);

    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        var argument = arguments[0];
        RunArguments.Add(argument);

        if (_hangingArguments.Contains(argument))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        var result = _results.TryGetValue(argument, out var configured)
            ? configured
            : new ProcessRunResult(1, [], []);
        return result;
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments)
    {
    }
}
