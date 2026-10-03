namespace Porchlight.Core.Browsers;

/// <summary>A browser setting that unwanted software commonly changes (see docs/specs/32-browser-hijack-check.md).</summary>
public enum HijackSetting
{
    HomePage,
    StartupPages,
    NewTabPage,
    SearchEngine,
}
