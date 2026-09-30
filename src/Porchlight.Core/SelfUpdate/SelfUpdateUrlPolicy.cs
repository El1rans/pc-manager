namespace Porchlight.Core.SelfUpdate;

/// <summary>Which URLs the self-updater will fetch an installer from: https only, on GitHub's own
/// release hosts. Checked before the first request and again for every redirect hop.</summary>
public static class SelfUpdateUrlPolicy
{
    private static readonly string[] AllowedDownloadHosts =
    [
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    /// <summary>True for an https URL on the default port, with no embedded credentials, whose host is
    /// one of GitHub's release download hosts.</summary>
    public static bool IsAllowedDownloadUrl(Uri? uri) =>
        uri is { IsAbsoluteUri: true }
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && AllowedDownloadHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    /// <summary>True for an https github.com URL - what a release's page link must be to be opened in a browser.</summary>
    public static bool IsGitHubHttps(Uri? uri) =>
        uri is { IsAbsoluteUri: true }
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
}
