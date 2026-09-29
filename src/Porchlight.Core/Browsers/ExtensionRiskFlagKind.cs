namespace Porchlight.Core.Browsers;

/// <summary>Kinds of finding <see cref="ExtensionRiskAssessor"/> can report for an add-on.</summary>
public enum ExtensionRiskFlagKind
{
    AllSites,
    History,
    Downloads,
    NativeMessaging,
    Proxy,
    Debugger,
    Management,
    NotFromStore,
    Policy,
    RecentlyInstalled,
}
