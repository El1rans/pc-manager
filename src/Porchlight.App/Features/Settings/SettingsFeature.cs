using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;

namespace Porchlight.App.Features.Settings;

/// <summary>Registers the Settings category's three pages (General, Notifications, Optional
/// features). See <c>docs/specs/22-nav-categories.md</c>.</summary>
public static class SettingsFeature
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services)
    {
        // Text size card on the General page (docs/specs/37-larger-text.md); real, also in demo mode.
        services.AddSingleton<ITextScaleService, TextScaleService>();
        services.AddTransient<TextSizeViewModel>();
        return services.AddSettingsPages();
    }

    private static IServiceCollection AddSettingsPages(this IServiceCollection services) => services
        .AddPage<GeneralSettingsViewModel, GeneralSettingsView>()
        .AddPage<NotificationSettingsViewModel, NotificationSettingsView>()
        .AddPage<OptionalFeaturesViewModel, OptionalFeaturesView>();
}
