using Microsoft.Extensions.DependencyInjection;
using PCManager.Core.Settings;

namespace PCManager.Core.Lighting;

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
        services.AddSingleton<ILightingService, LightingService>();
        return services;
    }
}
