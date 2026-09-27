using Microsoft.Extensions.DependencyInjection;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Lighting;

/// <summary>Registers the Lighting feature's Core services (see 05-lighting.md). Called from the
/// App layer's own <c>AddLightingFeature</c> alongside the page registration.</summary>
public static class LightingServiceCollectionExtensions
{
    /// <summary>OpenRGB.NET's own per-call socket timeout; kept well under
    /// <see cref="LightingService"/>'s 3 second call timeout so the client fails fast on its own
    /// rather than relying only on the outer guard.</summary>
    private const int OpenRgbSocketTimeoutMs = 2000;

    public static IServiceCollection AddLightingCore(this IServiceCollection services)
    {
        services.AddSingleton<IOpenRgbClient>(sp =>
        {
            var lighting = sp.GetRequiredService<ISettingsStore>().Current.Lighting;
            return new OpenRgbClientAdapter(lighting.OpenRgbHost, lighting.OpenRgbPort, OpenRgbSocketTimeoutMs);
        });

#if DEBUG
        // Non-shipping (DEBUG-only) escape hatch for capturing Lighting page documentation
        // screenshots without exposing the real machine's actual RGB devices - see
        // Monitoring.Demo.DemoDataMode and CONTRIBUTING.md's "Screenshots" section. Never connects
        // to a real OpenRGB SDK server. Has no effect, and the branch below does not exist at all,
        // in a Release build.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<ILightingService, Demo.DemoLightingService>();
            return services;
        }
#endif

        services.AddSingleton<ILightingService, LightingService>();
        return services;
    }
}
