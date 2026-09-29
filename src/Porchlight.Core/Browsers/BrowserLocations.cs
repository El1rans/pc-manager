namespace Porchlight.Core.Browsers;

/// <inheritdoc cref="IBrowserLocations"/>
public sealed class BrowserLocations : IBrowserLocations
{
    public BrowserLocations()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        ChromiumBrowsers =
        [
            new(BrowserKind.Edge, Path.Combine(local, "Microsoft", "Edge", "User Data")),
            new(BrowserKind.Chrome, Path.Combine(local, "Google", "Chrome", "User Data")),
            new(BrowserKind.Brave, Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")),
        ];
        FirefoxProfilesDirectory = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
    }

    public IReadOnlyList<ChromiumBrowserLocation> ChromiumBrowsers { get; }

    public string? FirefoxProfilesDirectory { get; }
}
