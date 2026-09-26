using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;

namespace PCManager.App.Features.Updates;

public static class UpdatesFeature
{
    public static IServiceCollection AddUpdatesFeature(this IServiceCollection services)
    {
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<IPage>(sp => sp.GetRequiredService<UpdatesViewModel>());
        return services;
    }
}
