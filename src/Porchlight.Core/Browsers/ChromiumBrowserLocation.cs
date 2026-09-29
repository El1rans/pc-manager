namespace Porchlight.Core.Browsers;

/// <summary>A Chromium-based browser's "User Data" folder.</summary>
public sealed record ChromiumBrowserLocation(BrowserKind Kind, string UserDataDirectory);
