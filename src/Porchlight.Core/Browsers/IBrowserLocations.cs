namespace Porchlight.Core.Browsers;

/// <summary>Where the supported browsers keep their profiles, behind an interface so tests can
/// point the scanner at a temp folder instead of the real machine's profiles.</summary>
public interface IBrowserLocations
{
    IReadOnlyList<ChromiumBrowserLocation> ChromiumBrowsers { get; }

    /// <summary>The Firefox "Profiles" folder, or null if not applicable.</summary>
    string? FirefoxProfilesDirectory { get; }
}
