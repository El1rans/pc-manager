using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Startup;

namespace Porchlight.App.Features.Notifications;

/// <summary>At startup, keeps the "start when I sign in" task pointing at the running exe (it goes
/// stale if Porchlight is moved or reinstalled elsewhere). Runs off the UI thread, best-effort:
/// re-registers only when already elevated, otherwise just logs. See
/// <c>docs/specs/24-start-at-login.md</c>.</summary>
public sealed class LoginLaunchRefreshHostedService(
    ILoginLaunchService loginLaunch,
    ILogger<LoginLaunchRefreshHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
#if DEBUG
        if (Porchlight.Core.Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            return Task.CompletedTask;
        }
#endif
        _ = Task.Run(RefreshAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RefreshAsync()
    {
        try
        {
            await loginLaunch.RefreshStaleRegistrationAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check the sign-in task.");
        }
    }
}
