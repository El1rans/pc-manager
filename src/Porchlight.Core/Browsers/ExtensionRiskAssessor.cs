namespace Porchlight.Core.Browsers;

/// <summary>
/// Turns an add-on's permissions, source, state and age into plain-language findings and one
/// overall level. Pure and deterministic (the clock is passed in). The wording says what an add-on
/// <i>can</i> do and never claims that anything is malware - see docs/specs/17-browser-extensions.md.
/// </summary>
public static class ExtensionRiskAssessor
{
    /// <summary>How recent an install must be to get the "Installed in the last 7 days" note.</summary>
    public static readonly TimeSpan RecentInstallWindow = TimeSpan.FromDays(7);

    private static readonly HashSet<string> AllSitesPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "<all_urls>",
        "*://*/*",
        "*://*/",
        "http://*/*",
        "http://*/",
        "https://*/*",
        "https://*/",
    };

    private static readonly string[] HistoryPermissions = ["history", "tabs", "webNavigation"];

    public static ExtensionRiskAssessment Assess(InstalledExtension extension, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extension);

        var flags = new List<ExtensionRiskFlag>();

        if (extension.HostPermissions.Concat(extension.Permissions).Any(p => AllSitesPatterns.Contains(p.Trim())))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.AllSites,
                "Can read and change all websites you visit",
                "It can see what you view and type on every page, including shopping and banking sites."));
        }

        if (HasPermission(extension, HistoryPermissions))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.History,
                "Can see your browsing history",
                "It can see which websites you open."));
        }

        if (HasPermission(extension, "downloads"))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.Downloads,
                "Can change downloads",
                "It can start or change file downloads."));
        }

        if (HasPermission(extension, "nativeMessaging"))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.NativeMessaging,
                "Can talk to programs on this PC",
                "It can exchange information with other programs installed on this PC."));
        }

        if (HasPermission(extension, "proxy"))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.Proxy,
                "Can change proxy settings",
                "It can send your web traffic through another computer."));
        }

        if (HasPermission(extension, "debugger"))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.Debugger,
                "Can debug pages",
                "It can inspect and change any web page in great detail."));
        }

        if (HasPermission(extension, "management"))
        {
            flags.Add(new(
                ExtensionRiskFlagKind.Management,
                "Can manage other add-ons",
                "It can turn your other add-ons on or off."));
        }

        if (extension.Source is ExtensionSource.Sideloaded or ExtensionSource.Developer or ExtensionSource.Unknown)
        {
            flags.Add(new(
                ExtensionRiskFlagKind.NotFromStore,
                "Not from the official store",
                "It was added some other way than through the browser's store, so it has not had the store's checks."));
        }

        if (extension.Source == ExtensionSource.Policy)
        {
            flags.Add(new(
                ExtensionRiskFlagKind.Policy,
                "Installed by a policy",
                "Some workplaces do this on purpose, but unwanted programs can also force an add-on onto a home PC. If nobody set this up for you, it is worth removing."));
        }

        if (extension.InstalledUtc is { } installed
            && installed <= now
            && now - installed <= RecentInstallWindow)
        {
            flags.Add(new(
                ExtensionRiskFlagKind.RecentlyInstalled,
                "Installed in the last 7 days",
                "If you did not add it yourself, it is worth finding out where it came from."));
        }

        return new ExtensionRiskAssessment(LevelFor(extension, flags), flags);
    }

    private static ExtensionRiskLevel LevelFor(InstalledExtension extension, List<ExtensionRiskFlag> flags)
    {
        var hasPowerful = flags.Any(f => IsPowerful(f.Kind));
        if (hasPowerful && extension.Enabled && extension.Source != ExtensionSource.Store)
        {
            return ExtensionRiskLevel.WorthRemoving;
        }

        return flags.Any(f => !IsNote(f.Kind)) ? ExtensionRiskLevel.Review : ExtensionRiskLevel.LooksFine;
    }

    private static bool IsPowerful(ExtensionRiskFlagKind kind) => kind is
        ExtensionRiskFlagKind.AllSites or
        ExtensionRiskFlagKind.History or
        ExtensionRiskFlagKind.Proxy or
        ExtensionRiskFlagKind.NativeMessaging or
        ExtensionRiskFlagKind.Debugger or
        ExtensionRiskFlagKind.Management;

    /// <summary>Findings that are worth showing but do not, on their own, lift the level above
    /// "Looks fine".</summary>
    private static bool IsNote(ExtensionRiskFlagKind kind) => kind is
        ExtensionRiskFlagKind.Downloads or
        ExtensionRiskFlagKind.RecentlyInstalled;

    private static bool HasPermission(InstalledExtension extension, params string[] names) =>
        extension.Permissions.Any(p => names.Contains(p, StringComparer.OrdinalIgnoreCase));
}
