using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

/// <summary>
/// Starts/stops <see cref="IHardwareService"/> with the app's lifetime, and covers spec 04's rule
/// 5 ("restore on exit, crash handler, system suspend, and session end"): app exit and crash both
/// go through <see cref="StopAsync"/> (the host's normal shutdown path, and
/// <c>App.OnDispatcherUnhandledException</c>'s best-effort call - see <c>App.xaml.cs</c>), while
/// suspend and session-end are handled here directly via <see cref="SystemEvents"/>, since neither
/// stops the host.
/// </summary>
public sealed class HardwareHostedService : IHostedService
{
    private readonly IHardwareService _hardwareService;
    private readonly FanControlManager _fanControlManager;
    private readonly ILogger<HardwareHostedService> _logger;

    public HardwareHostedService(
        IHardwareService hardwareService, FanControlManager fanControlManager, ILogger<HardwareHostedService> logger)
    {
        _hardwareService = hardwareService;
        _fanControlManager = fanControlManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        _hardwareService.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
        RestoreAllBestEffort("app shutdown");
        _hardwareService.Stop();
        return Task.CompletedTask;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            RestoreAllBestEffort("system suspend");
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => RestoreAllBestEffort("session ending");

    private void RestoreAllBestEffort(string reason)
    {
        try
        {
            _fanControlManager.RestoreAll();
        }
        catch (Exception ex)
        {
            // Best-effort safety net: the process may already be tearing down (suspend/session-end
            // handlers run with little time), so log and move on rather than throw from here.
            _logger.LogError(ex, "Could not restore fans to default control on {Reason}.", reason);
        }
    }
}
