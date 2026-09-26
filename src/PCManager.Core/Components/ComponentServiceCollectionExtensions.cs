using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IComponentService, ComponentService>();
        return services;
    }
}
