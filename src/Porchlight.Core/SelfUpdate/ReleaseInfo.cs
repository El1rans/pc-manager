namespace Porchlight.Core.SelfUpdate;

/// <summary>A published Porchlight release, as read from GitHub's "latest release" API. See
/// <see cref="ReleaseParser"/>.</summary>
/// <param name="Version">The version from the release's <c>vX.Y.Z</c> tag.</param>
/// <param name="Tag">The tag name as published (<c>v0.2.0</c>).</param>
/// <param name="ReleasePageUrl">The release's page on github.com (always an https github.com URL).</param>
/// <param name="Notes">The release notes (markdown), if any.</param>
/// <param name="InstallerUrl">Download URL of <c>Porchlight-Setup-X.Y.Z.exe</c>, or null if the release has no such asset.</param>
/// <param name="ChecksumUrl">Download URL of the installer's <c>.sha256</c> file, or null if missing.</param>
public sealed record ReleaseInfo(
    Version Version, string Tag, Uri ReleasePageUrl, string? Notes, Uri? InstallerUrl, Uri? ChecksumUrl)
{
    /// <summary>The installer file name the release workflow publishes (<c>Porchlight-Setup-X.Y.Z.exe</c>).</summary>
    public string InstallerFileName => ReleaseParser.InstallerFileNameFor(Version);

    /// <summary>True when both the installer and its checksum are attached, so it can be updated in-app.</summary>
    public bool CanInstallAutomatically => InstallerUrl is not null && ChecksumUrl is not null;
}
