namespace Porchlight.Core.Browsers;

/// <summary>Reads the browser policies in the registry, behind an interface so scanning is testable
/// without a registry.</summary>
public interface IBrowserPolicyReader
{
    /// <summary>Policy-forced settings for <paramref name="kind"/> (machine and user policies merged,
    /// machine first); <see cref="BrowserPolicySettings.None"/> when there are none, or for Firefox.</summary>
    BrowserPolicySettings Read(BrowserKind kind);
}
