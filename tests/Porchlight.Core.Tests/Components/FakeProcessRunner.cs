using Porchlight.Core.Processes;

namespace Porchlight.Core.Tests.Components;

/// <summary>
/// Shared <see cref="IProcessRunner"/> fake. By default every <see cref="RunAsync"/> returns the
/// single result described by <see cref="NextExitCode"/> / <see cref="NextLines"/> /
/// <see cref="NextErrorLines"/>. Alternatively, script per-argument results (keyed on the first CLI
/// argument, e.g. <c>--get-id</c>) with <see cref="SetResult"/> / <see cref="SetLines"/>; once any
/// are scripted, unscripted arguments yield exit code 1 with no output. <see cref="Hang"/> and
/// <see cref="RunException"/> simulate a call that never completes or that throws.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessRunResult> _results = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hangingArguments = new(StringComparer.Ordinal);

    public int NextExitCode { get; set; }

    public IReadOnlyList<string> NextLines { get; set; } = [];

    public IReadOnlyList<string> NextErrorLines { get; set; } = [];

    /// <summary>When set, <see cref="RunAsync"/> throws it (after recording the call).</summary>
    public Exception? RunException { get; set; }

    /// <summary>Records the cancellation token each <see cref="RunAsync"/> call actually received,
    /// so tests can assert that an install passes <see cref="CancellationToken.None"/> once
    /// launched (see <c>ComponentService.InstallAsync</c>).</summary>
    public List<CancellationToken> RunCancellationTokens { get; } = [];

    /// <summary>Every install/run invocation, so tests can assert on the exact winget arguments used.</summary>
    public List<(string FileName, IReadOnlyList<string> Arguments)> RunCalls { get; } = [];

    /// <summary>The first argument of every <see cref="RunAsync"/> call, in order.</summary>
    public IEnumerable<string> RunArguments => RunCalls.Select(call => call.Arguments[0]);

    public List<(string FileName, IReadOnlyList<string> Arguments)> StartDetachedCalls { get; } = [];

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
        RunCalls.Add((fileName, arguments));
        RunCancellationTokens.Add(cancellationToken);

        if (RunException is not null)
        {
            throw RunException;
        }

        if (arguments.Count > 0 && _hangingArguments.Contains(arguments[0]))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        if (_results.Count > 0)
        {
            return arguments.Count > 0 && _results.TryGetValue(arguments[0], out var configured)
                ? configured
                : new ProcessRunResult(1, [], []);
        }

        return new ProcessRunResult(NextExitCode, NextLines, NextErrorLines);
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
        StartDetachedCalls.Add((fileName, arguments));
}
