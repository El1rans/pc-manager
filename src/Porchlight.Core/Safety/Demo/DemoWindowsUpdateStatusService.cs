namespace Porchlight.Core.Safety.Demo;

/// <summary>DEBUG demo data: updated nine days ago, one failed attempt.</summary>
internal sealed class DemoWindowsUpdateStatusService : IWindowsUpdateStatusService
{
    public Task<WindowsUpdateStatus> GetAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(UpdateVerdictBuilder.Build(
            new UpdateHistorySummary(now.AddDays(-9), 1), rebootPending: false, now));
    }

    public Task<PendingUpdatesCheck> CheckPendingAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new PendingUpdatesCheck(PendingUpdatesOutcome.Found, 2));
}
