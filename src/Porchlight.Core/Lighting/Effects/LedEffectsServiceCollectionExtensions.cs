using Microsoft.Extensions.DependencyInjection;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Lighting.Effects;

/// <summary>Registers the LED effects engine's Core services (see docs/specs/11-led-effects.md).
/// Called from the composition root alongside the other <c>Add&lt;Feature&gt;...</c> calls; phase 1
/// has no App-layer page, so there is no matching <c>AddLedEffectsFeature</c> yet.</summary>
public static class LedEffectsServiceCollectionExtensions
{
    /// <summary>Same reasoning as <c>LightingServiceCollectionExtensions</c>'s own socket timeout:
    /// kept short so a stalled OpenRGB call fails fast.</summary>
    private const int OpenRgbSocketTimeoutMs = 2000;

    public static IServiceCollection AddLedEffectsCore(this IServiceCollection services)
    {
        services.AddSingleton<IEffectDeviceClient>(sp =>
        {
            var lighting = sp.GetRequiredService<ISettingsStore>().Current.Lighting;
            return new OpenRgbEffectDeviceClient(lighting.OpenRgbHost, lighting.OpenRgbPort, OpenRgbSocketTimeoutMs);
        });

        services.AddSingleton<IDeviceExclusionProvider, SettingsDeviceExclusionProvider>();
        services.AddSingleton<IPendingUpdateCountProvider, ZeroPendingUpdateCountProvider>();
        services.AddSingleton<IKeyPressSource, NullKeyPressSource>();
        services.AddSingleton<EffectEngine>();

        return services;
    }
}
