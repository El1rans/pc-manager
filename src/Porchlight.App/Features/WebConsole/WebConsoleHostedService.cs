using Microsoft.Extensions.Hosting;
using Porchlight.Core.WebConsole;

namespace Porchlight.App.Features.WebConsole;

/// <summary>Resumes the web console at app startup if the user left it on, and stops it (closing
/// the listening port) at shutdown.</summary>
public sealed class WebConsoleHostedService(WebConsoleController controller) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        controller.ApplySettings();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        controller.Server.Stop();
        return Task.CompletedTask;
    }
}
