namespace Porchlight.Core.Browsers;

/// <summary>How an add-on got into the browser.</summary>
public enum ExtensionSource
{
    /// <summary>Unknown - treated like <see cref="Sideloaded"/> for risk purposes.</summary>
    Unknown,

    /// <summary>The browser's official add-on store (Firefox: signed by Mozilla).</summary>
    Store,

    /// <summary>Added some other way than the store (another program, a downloaded file).</summary>
    Sideloaded,

    /// <summary>Forced onto the browser by a policy.</summary>
    Policy,

    /// <summary>Loaded from a folder in developer mode (unpacked / temporary).</summary>
    Developer,
}
