namespace Porchlight.Core.Safety;

/// <summary>The three Windows Firewall network profiles. Values are the <c>NET_FW_PROFILE_TYPE2</c> bit flags.</summary>
[Flags]
public enum WindowsFirewallProfile
{
    None = 0,
    Domain = 1,
    Private = 2,
    Public = 4,
}
