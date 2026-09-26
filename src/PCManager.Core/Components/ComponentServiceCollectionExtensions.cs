using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PCManager.Core.Elevation;
using PCManager.Core.Processes;

namespace PCManager.Core.Components;

/// <summary>Registers the shared component detection/install/start services used by every feature
/// that depends on a third-party tool (AnyDesk, OpenRGB, the PawnIO driver).</summary>
public static class ComponentServiceCollectionExtensions
{
    public static IServiceCollection AddComponents(this IServiceCollection services)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IRegistryReader, RegistryReader>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IProcessProbe, ProcessProbe>();
        // TryAdd: the host may already register IElevationService itself (it is also used
        // directly by the shell for the "restart as admin" button); either way there is exactly
        // one instance.
        services.TryAddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<IComponentService, ComponentService>();
        return services;
    }
}
