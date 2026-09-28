using CommunityToolkit.Mvvm.Input;

namespace Porchlight.App.Shell;

/// <summary>
/// Shared admin/elevation state and the single "restart elevated" command, used by both the
/// sidebar footer (<see cref="MainViewModel"/>) and any feature page's
/// <see cref="Porchlight.App.Controls.AdminRequiredBanner"/>, so the relaunch-and-shutdown
/// behaviour lives in exactly one place.
/// </summary>
public interface IShellService
{
    bool IsElevated { get; }

    IRelayCommand RestartElevatedCommand { get; }
}
