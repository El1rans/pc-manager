namespace Porchlight.Core.RemoveApps;

/// <summary>Raised after an app was removed (see <see cref="IRemoveAppsService.AppRemoved"/>).</summary>
public sealed class AppRemovedEventArgs(RemovableApp app) : EventArgs
{
    public RemovableApp App { get; } = app;
}
