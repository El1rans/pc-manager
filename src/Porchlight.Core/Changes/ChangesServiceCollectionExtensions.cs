using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.Changes;

/// <summary>Registers the change journal and the automatic restore point (spec 34). The undoers
/// themselves are registered by the features that own them.</summary>
public static class ChangesServiceCollectionExtensions
{
    public static IServiceCollection AddChangesCore(this IServiceCollection services)
    {
        // Real even in demo mode: the restore point service it calls is faked there, and the demo
        // data folder is separate from the real one.
        services.AddSingleton<IAutoRestorePoint, AutoRestorePoint>();

#if DEBUG
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IChangeJournal, Demo.DemoChangeJournal>();
            return services;
        }
#endif

        services.AddSingleton<IChangeJournal, ChangeJournal>();
        return services;
    }
}
