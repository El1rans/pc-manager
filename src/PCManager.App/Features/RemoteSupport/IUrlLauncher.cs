namespace PCManager.App.Features.RemoteSupport;

/// <summary>Opens a URL in the user's default browser, behind an interface so the "download
/// AnyDesk yourself" fallback link is unit-testable without actually launching a browser.</summary>
public interface IUrlLauncher
{
    /// <summary>Opens <paramref name="url"/> in the default browser. Never throws; a failure is
    /// logged and otherwise ignored.</summary>
    void Open(string url);
}
