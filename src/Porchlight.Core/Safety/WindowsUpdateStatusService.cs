namespace Porchlight.Core.Safety;

/// <inheritdoc cref="IWindowsUpdateStatusService"/>
public sealed class WindowsUpdateStatusService(IWindowsUpdateAgent agent, TimeProvider time) : IWindowsUpdateStatusService
{
    public async Task<WindowsUpdateStatus> GetAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var history = await agent.ReadHistoryAsync(cancellationToken).ConfigureAwait(false);
        var reboot = await agent.IsRestartPendingAsync(cancellationToken).ConfigureAwait(false);
        var summary = history is null ? null : UpdateHistorySummarizer.Summarize(history, now);
        return UpdateVerdictBuilder.Build(summary, reboot, now);
    }

    public async Task<PendingUpdatesCheck> CheckPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await agent.CountPendingAsync(cancellationToken)
                .WaitAsync(SafetyTimeouts.PendingUpdateSearch, cancellationToken).ConfigureAwait(false);
            return count is { } c
                ? new PendingUpdatesCheck(PendingUpdatesOutcome.Found, c)
                : new PendingUpdatesCheck(PendingUpdatesOutcome.Failed, 0);
        }
        catch (TimeoutException)
        {
            return new PendingUpdatesCheck(PendingUpdatesOutcome.TimedOut, 0);
        }
    }
}
