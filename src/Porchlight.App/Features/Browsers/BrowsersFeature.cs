using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Browsers;

namespace Porchlight.App.Features.Browsers;

public static class BrowsersFeature
{
    public static IServiceCollection AddBrowsersFeature(this IServiceCollection services)
    {
        services.AddBrowsersCore();
        return services.AddPage<BrowserExtensionsViewModel, BrowserExtensionsView>();
    }
}
