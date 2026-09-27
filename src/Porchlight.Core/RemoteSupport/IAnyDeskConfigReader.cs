namespace Porchlight.Core.RemoteSupport;

/// <summary>
/// The narrow slice of file-system access <see cref="AnyDeskService"/> needs to fall back to
/// AnyDesk's own config files when its <c>--get-id</c>/<c>--get-alias</c> CLI calls fail, behind an
/// interface so that fallback is unit-testable without touching the real disk.
/// </summary>
public interface IAnyDeskConfigReader
{
    /// <summary>Reads the full text of AnyDesk's config file at <paramref name="path"/>, or null if
    /// it does not exist or could not be read.</summary>
    string? TryRead(string path);

    /// <summary><c>%ProgramData%\AnyDesk\system.conf</c> - the primary config file, tried first.</summary>
    string SystemConfPath { get; }

    /// <summary><c>%ProgramData%\AnyDesk\service.conf</c> - fallback when <see cref="SystemConfPath"/>
    /// has no ID (older AnyDesk versions store it here instead).</summary>
    string ServiceConfPath { get; }
}
