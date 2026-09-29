namespace Porchlight.Core.Browsers;

/// <summary>Finds a browser's executable, behind an interface so opening add-ons pages is testable
/// without a registry.</summary>
public interface IBrowserExecutableLocator
{
    /// <summary>Full path of the browser's exe, or null if it is not installed.</summary>
    string? Find(BrowserKind kind);
}
