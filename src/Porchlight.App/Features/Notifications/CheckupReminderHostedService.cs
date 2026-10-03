using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.App.Tray;
using Porchlight.Core.Checkup;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Notifications;

/// <summary>Background loop: while Porchlight runs (window open or in the tray) it checks every few
/// minutes whether the check-up reminder is due and, if so, shows one balloon whose click opens the
/// Get help page where the report is made. Porchlight never sends anything itself. Off in DEBUG
/// demo mode so demo runs never raise real toasts. See <c>docs/specs/38-checkup-reminder.md</c>.</summary>
public sealed class CheckupReminderHostedService(
    CheckupReminderScheduler scheduler,
    ISettingsStore settingsStore,
    ITrayIcon trayIcon,
    IShellWindowService shell,
    ILogger<CheckupReminderHostedService> logger,
    TimeProvider timeProvider) : IHostedService, IDisposable
{
    /// <summary>Wait after start so the reminder never competes with startup work.</summary>
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public const string BalloonTitle = "Time for a check-up";

    public const string BalloonMessage = "Send a quick check-up of this PC to the person who helps you. Click here to make one.";

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

    /// <summary>Shows the reminder if it is due and records that it was shown. Returns true if shown.</summary>
    public bool CheckOnce()
    {
        try
        {
            var reminder = settingsStore.Current.CheckupReminder;
            if (!scheduler.IsDue(reminder))
            {
                return false;
            }

            trayIcon.ShowBalloon(BalloonTitle, BalloonMessage, () => shell.NavigateTo(typeof(RemoteSupportViewModel)));
            var now = timeProvider.GetUtcNow();
            settingsStore.Update(s => s.CheckupReminder.LastReminderShownUtc = now);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad round must not end the loop; the next tick tries again.
            logger.LogError(ex, "Check-up reminder check failed.");
            return false;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InitialDelay, timeProvider, cancellationToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval, timeProvider);
            do
            {
                CheckOnce();
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Shutting down; expected.
        }
    }
}
