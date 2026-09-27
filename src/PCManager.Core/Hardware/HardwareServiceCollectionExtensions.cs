using Microsoft.Extensions.DependencyInjection;

namespace PCManager.Core.Hardware;

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
        services.AddSingleton<IHardwareService, HardwareService>();
        services.AddSingleton(sp => new FanControlEngine(sp.GetRequiredService<IClock>()));
        services.AddSingleton<IFanControlActivityMarker, FanControlActivityMarker>();
        services.AddSingleton<FanControlManager>();
        return services;
    }
}
