using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Updates;
using Porchlight.Core.Alerts;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Notifications;

/// <summary>Runs the update check on the user's schedule (daily by default) and raises the "updates
/// are ready" alert. Checks only, never installs. Reuses the Updates page's own winget path via
/// <see cref="IUpdateChecker"/>. Off in DEBUG demo mode.</summary>
public sealed class ScheduledUpdateCheckHostedService(
    IUpdateChecker updateChecker,
    AlertEvaluator evaluator,
    AlertNotifier notifier,
    ISettingsStore settingsStore,
    ILogger<ScheduledUpdateCheckHostedService> logger,
    TimeProvider? timeProvider = null) : IHostedService, IDisposable
{
    /// <summary>Waits past the startup check (<c>UpdatesAutoCheckHostedService</c>) before the
    /// first schedule decision, so the two never run back to back.</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private CancellationTokenSource? _cts;
    private Task _loop = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
#if DEBUG
        if (Porchlight.Core.Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            return Task.CompletedTask;
        }
#endif
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown was asked to stop waiting; the loop ends on its own.
        }

    }

    public void Dispose() => _cts?.Dispose();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InitialDelay, _time, cancellationToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval, _time);
            do
            {
                await CheckIfDueAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Shutting down; expected.
        }
    }

    private async Task CheckIfDueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = settingsStore.Current.Notifications;
            var now = _time.GetUtcNow();
            if (!UpdateCheckPolicy.IsDue(settings.UpdateCheckSchedule, settings.LastScheduledUpdateCheckUtc, now))
            {
                return;
            }

            var result = await updateChecker.CheckAsync(cancellationToken).ConfigureAwait(false);
            if (result.Status != UpdateCheckStatus.Succeeded)
            {
                // Busy, no winget or a failure: not recorded, so the next hour retries.
                logger.LogInformation("Scheduled update check did not complete; will retry later.");
                return;
            }

            settingsStore.Update(s => s.Notifications.LastScheduledUpdateCheckUtc = now);
            var alert = evaluator.EvaluateUpdates(result.Count, AlertPreferences.FromSettings(settingsStore.Current.Notifications));
            if (alert is not null)
            {
                notifier.Show(alert);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scheduled update check failed.");
        }
    }
}
