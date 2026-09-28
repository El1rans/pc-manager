using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.Hardware;

/// <summary>Registers every Core-side hardware/fan-control service as a singleton: one
/// <see cref="HardwareService"/> owns the hardware library for the app's lifetime, one
/// <see cref="FanControlEngine"/> keeps its safety state (hysteresis, sticky failsafes) across
/// ticks, and one <see cref="FanControlManager"/> wires them to settings. The App project adds a
/// small hosted service (see <c>HardwareHostedService</c>) that starts/stops
/// <see cref="IHardwareService"/> and hooks system suspend/session-end.</summary>
public static class HardwareServiceCollectionExtensions
{
    public static IServiceCollection AddHardwareCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

#if DEBUG
        // Non-shipping (DEBUG-only) escape hatch for capturing Hardware page documentation
        // screenshots without exposing the real machine's sensors - see
        // Monitoring.Demo.DemoDataMode and CONTRIBUTING.md's "Screenshots" section. Never opens
        // the real hardware library and never controls a real fan. Has no effect, and the branch
        // below does not exist at all, in a Release build.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IHardwareService, Demo.DemoHardwareService>();
        }
        else
        {
            services.AddSingleton<IHardwareService, HardwareService>();
        }
#else
        services.AddSingleton<IHardwareService, HardwareService>();
#endif

        services.AddSingleton(sp => new FanControlEngine(sp.GetRequiredService<IClock>()));
        services.AddSingleton<IFanControlActivityMarker, FanControlActivityMarker>();
        services.AddSingleton<FanControlManager>();
        services.AddSingleton<IRunningSoftwareLister, RunningSoftwareLister>();
        services.AddSingleton<IFanControlConflictDetector, FanControlConflictDetector>();
        return services;
    }
}
