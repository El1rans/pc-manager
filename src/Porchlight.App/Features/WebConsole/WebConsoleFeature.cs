using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.WebConsole;

namespace Porchlight.App.Features.WebConsole;

public static class WebConsoleFeature
{
    /// <summary>Registers the read-only web console (docs/specs/20-web-console.md). Relies on the
    /// Dashboard and Hardware features' Core services (monitoring, hardware) and on the "Get help"
    /// feature's <c>IClipboardService</c>/<c>IUrlLauncher</c>.</summary>
    public static IServiceCollection AddWebConsoleFeature(this IServiceCollection services)
    {
        services.AddWebConsoleCore();
        services.AddHostedService<WebConsoleHostedService>();
        return services.AddPage<WebConsoleViewModel, WebConsoleView>();
    }
}
