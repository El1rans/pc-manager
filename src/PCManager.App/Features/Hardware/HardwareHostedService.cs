using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

/// <summary>
/// Starts/stops <see cref="IHardwareService"/> with the app's lifetime, and covers spec 04's rule
/// 5 ("restore on exit, crash handler, system suspend, and session end"): app exit goes through
/// <see cref="StopAsync"/> (the host's normal shutdown path); crash goes through
/// <c>App</c>'s unhandled-exception handlers (see <c>App.xaml.cs</c>); suspend and session-end are
/// handled here directly via <see cref="SystemEvents"/>, since neither stops the host.
/// </summary>
/// <remarks>
/// B2: every one of these calls <see cref="FanControlManager.Suspend"/>, not
/// <see cref="FanControlManager.RestoreAll"/> directly - a plain restore is undone by the very next
/// hardware-thread tick (worst case well under a second on Modern Standby, since the machine can
/// resume almost immediately), because nothing tells the engine to stop re-applying its last
/// target. <c>Suspend</c> pauses evaluation *and* restores, and only <see cref="OnPowerModeChanged"/>
/// resuming clears that pause - exit and session-end intentionally leave it paused, since the app
/// is going away anyway (or the user must explicitly re-arm from the page next launch).
/// </remarks>
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
        // N5: app exit is not a resumable system suspend - stays paused (there is nothing to resume
        // it anyway, since the process is exiting).
        SuspendBestEffort("app shutdown", resumableBySystemResume: false);
        _hardwareService.Stop();
        return Task.CompletedTask;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                SuspendBestEffort("system suspend", resumableBySystemResume: true);
                break;
            case PowerModes.Resume:
                try
                {
                    _fanControlManager.ResumeFromSuspend();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resuming fan control after system resume.");
                }

                break;
        }
    }

    // N5: session-ending is not a resumable system suspend either - stays paused until the user
    // explicitly re-arms from the Hardware page (which will not happen before the session ends, but
    // matches the same reasoning as app shutdown above for consistency).
    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => SuspendBestEffort("session ending", resumableBySystemResume: false);

    private void SuspendBestEffort(string reason, bool resumableBySystemResume)
    {
        try
        {
            _fanControlManager.Suspend(reason, resumableBySystemResume);
        }
        catch (Exception ex)
        {
            // Best-effort safety net: the process may already be tearing down (suspend/session-end
            // handlers run with little time), so log and move on rather than throw from here.
            _logger.LogError(ex, "Could not pause/restore fans to default control on {Reason}.", reason);
        }
    }
}
