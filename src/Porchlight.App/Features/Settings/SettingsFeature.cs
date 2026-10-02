using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;

namespace Porchlight.App.Features.Settings;

/// <summary>Registers the Settings category's three pages (General, Notifications, Optional
/// features). See <c>docs/specs/22-nav-categories.md</c>.</summary>
public static class SettingsFeature
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services) => services
        .AddPage<GeneralSettingsViewModel, GeneralSettingsView>()
        .AddPage<NotificationSettingsViewModel, NotificationSettingsView>()
        .AddPage<OptionalFeaturesViewModel, OptionalFeaturesView>();
}
