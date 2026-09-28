using Porchlight.Core.Winget;

namespace Porchlight.App.Tests.Features.Updates;

/// <summary>Fake <see cref="IWingetClient"/> for <see cref="UpdatesViewModelTests"/> - never runs a
/// real <c>winget</c> process.</summary>
internal sealed class FakeWingetClient : IWingetClient
{
    /// <summary>Successive results for <see cref="GetUpgradesAsync"/> calls, one queue entry per
    /// call (a manual refresh, then the automatic quiet re-check after an update run, etc.). The
    /// last entry is reused once the queue is exhausted.</summary>
    public Queue<IReadOnlyList<WingetPackage>> UpgradeListResults { get; } = new();

    private IReadOnlyList<WingetPackage> _lastUpgradeListResult = [];

    /// <summary>Per-package-id result for <see cref="UpgradeAsync"/>; defaults to a plain success.</summary>
    public Dictionary<string, WingetResult> UpgradeResultsById { get; } = new(StringComparer.OrdinalIgnoreCase);

    public WingetResult UninstallResult { get; set; } = new(0, []);

    public WingetResult InstallResult { get; set; } = new(0, []);

    /// <summary>When set, <see cref="UpgradeAsync"/> and <see cref="InstallAsync"/> await this task
    /// before returning - lets a test hold a package "in flight" to drive
    /// <c>UpdatesViewModel.WaitWithHintAsync</c> deterministically with a
    /// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> instead of a real delay.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Invoked synchronously just before <see cref="UpgradeAsync"/> returns its result -
    /// lets a test simulate the user clicking "Stop after current" while a package is mid-update.</summary>
    public Action<string>? OnUpgrading { get; set; }

    public List<string> UpgradeCalls { get; } = [];

    public List<bool> UpgradeSilentFlags { get; } = [];

    public List<string> UninstallCalls { get; } = [];

    public List<string> InstallCalls { get; } = [];

    /// <summary>Number of <see cref="GetUpgradesAsync"/> calls made so far - lets a test assert
    /// that a refresh did (or, per B1, deliberately did not) happen at some point.</summary>
    public int GetUpgradesCallCount { get; private set; }

    public Task<IReadOnlyList<WingetPackage>> GetUpgradesAsync(
        bool includeUnknown, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        GetUpgradesCallCount++;
        if (UpgradeListResults.Count > 0)
        {
            _lastUpgradeListResult = UpgradeListResults.Dequeue();
        }

        return Task.FromResult(_lastUpgradeListResult);
    }

    public async Task<WingetResult> UpgradeAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        UpgradeCalls.Add(id);
        UpgradeSilentFlags.Add(silent);
        OnUpgrading?.Invoke(id);

        if (Gate is not null)
        {
            await Gate.Task.ConfigureAwait(true);
        }

        return UpgradeResultsById.TryGetValue(id, out var configured) ? configured : new WingetResult(0, []);
    }

    public Task<WingetResult> ShowAsync(string id, IProgress<string>? log, CancellationToken cancellationToken) =>
        Task.FromResult(new WingetResult(0, []));

    public Task<WingetResult> UninstallAsync(string id, bool silent, IProgress<string>? log, CancellationToken cancellationToken)
    {
        UninstallCalls.Add(id);
        return Task.FromResult(UninstallResult);
    }

    public async Task<WingetResult> InstallAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        InstallCalls.Add(id);

        if (Gate is not null)
        {
            await Gate.Task.ConfigureAwait(true);
        }

        return InstallResult;
    }
}
