using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Alerts;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Notifications;

/// <summary>Background loop: every minute gathers the PC's health, asks <see cref="AlertEvaluator"/>
/// what (if anything) to say, and shows it via <see cref="AlertNotifier"/>. Off in DEBUG demo mode
/// so demo data never raises real toasts.</summary>
public sealed class AlertHostedService(
    IAlertInputProvider inputProvider,
    AlertEvaluator evaluator,
    AlertNotifier notifier,
    ISettingsStore settingsStore,
    ILogger<AlertHostedService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>Gap between two balloons raised in the same round, so each is readable before the next.</summary>
    private static readonly TimeSpan BalloonSpacing = TimeSpan.FromSeconds(8);

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
            await Task.Delay(InitialDelay, cancellationToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await EvaluateOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Shutting down; expected.
        }
    }

    private async Task EvaluateOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var preferences = AlertPreferences.FromSettings(settingsStore.Current.Notifications);
            var alerts = evaluator.Evaluate(inputProvider.GetInputs(), preferences);
            for (var i = 0; i < alerts.Count; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(BalloonSpacing, cancellationToken).ConfigureAwait(false);
                }

                notifier.Show(alerts[i]);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad round must not end the loop; the next tick tries again.
            logger.LogError(ex, "Background alert check failed.");
        }
    }
}
