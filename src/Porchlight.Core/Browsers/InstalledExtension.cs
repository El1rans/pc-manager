namespace Porchlight.Core.Browsers;

/// <summary>Facts about one installed add-on, as read (read-only) from a browser profile.</summary>
/// <param name="Browser">Which browser it is installed in.</param>
/// <param name="ProfileName">Friendly profile name (e.g. "Person 1").</param>
/// <param name="Id">The browser's id for the add-on.</param>
/// <param name="Name">Display name, with any localisation placeholder already resolved.</param>
/// <param name="Description">Short description, or empty.</param>
/// <param name="Version">Version text, or empty.</param>
/// <param name="Enabled">Whether the browser currently runs it.</param>
/// <param name="InstalledUtc">When it was installed, if known.</param>
/// <param name="Source">How it got there.</param>
/// <param name="Permissions">API permissions it requests (e.g. "history").</param>
/// <param name="HostPermissions">Website patterns it requests (e.g. "&lt;all_urls&gt;").</param>
public sealed record InstalledExtension(
    BrowserKind Browser,
    string ProfileName,
    string Id,
    string Name,
    string Description,
    string Version,
    bool Enabled,
    DateTimeOffset? InstalledUtc,
    ExtensionSource Source,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> HostPermissions);
