#if DEBUG
using Porchlight.Core.Cleanup;

namespace Porchlight.Core.RemoveApps.Demo;

/// <summary>DEBUG-only fake <see cref="IRemoveAppsService"/>: made-up apps, and removing one only
/// hides it from the list - see <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoRemoveAppsService : IRemoveAppsService
{
    private readonly List<RemovableApp> _apps =
    [
        Demo("Demo Photo Studio", "Example Software", "4.2", 4_800L * 1024 * 1024, new DateOnly(2021, 3, 14), RemovableAppKind.Normal, false),
        Demo("Sample Video Editor", "Sample Co", "12.0", 2_100L * 1024 * 1024, new DateOnly(2023, 8, 2), RemovableAppKind.Normal, false),
        Demo("Example Antivirus Trial", "Example Security", "3.1", 650L * 1024 * 1024, new DateOnly(2020, 1, 5), RemovableAppKind.Normal, true),
        Demo("Tiny Utility", null, null, null, null, RemovableAppKind.Normal, false),
        Demo("AnyDesk", "AnyDesk Software", "9.0", 12L * 1024 * 1024, new DateOnly(2024, 5, 1), RemovableAppKind.ManagedByPorchlight, false),
        Demo("Microsoft Visual C++ 2015-2022 Redistributable (x64)", "Microsoft Corporation", "14.40", 20L * 1024 * 1024, new DateOnly(2022, 2, 2), RemovableAppKind.SystemPart, false),
    ];

    public event EventHandler<AppRemovedEventArgs>? AppRemoved;

    public Task<IReadOnlyList<RemovableApp>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RemovableApp>>([.. _apps]);

    public Task<RemoveAppOutcome> RemoveAsync(RemovableApp app, CancellationToken cancellationToken)
    {
        if (!app.CanRemove || !_apps.Remove(app))
        {
            return Task.FromResult(new RemoveAppOutcome(RemoveAppResult.Refused));
        }

        AppRemoved?.Invoke(this, new AppRemovedEventArgs(app));
        return Task.FromResult(new RemoveAppOutcome(RemoveAppResult.Removed));
    }

    private static RemovableApp Demo(
        string name, string? publisher, string? version, long? size, DateOnly? date, RemovableAppKind kind, bool preinstalled) =>
        new(new InstalledApp(name, publisher, version, size, date, "demo.exe", true), kind, preinstalled, null);
}
#endif
