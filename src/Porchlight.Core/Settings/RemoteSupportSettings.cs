namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the "Get help" (remote support) feature.</summary>
public sealed class RemoteSupportSettings
{
    /// <summary>Optional first name of the family member who helps this user, shown on the "Get
    /// help" page as "Your helper: &lt;name&gt;". Empty (the default) hides that line entirely.</summary>
    public string HelperName { get; set; } = string.Empty;
}
