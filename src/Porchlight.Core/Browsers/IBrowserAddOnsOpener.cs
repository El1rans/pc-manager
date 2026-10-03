namespace Porchlight.Core.Browsers;

/// <summary>Opens a browser's own add-ons page so the user can remove an add-on there.</summary>
public interface IBrowserAddOnsOpener
{
    /// <summary>True if the browser was started; false if it could not be found or started.</summary>
    bool TryOpen(BrowserKind kind);

    /// <summary>Opens the browser's own settings page for <paramref name="setting"/>. True if the browser
    /// was started; false if it could not be found or started.</summary>
    bool TryOpenSettings(BrowserKind kind, HijackSetting setting);
}
