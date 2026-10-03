namespace Porchlight.Core.RemoveApps;

/// <summary>Programs that commonly come with a new PC (trial security software, game bundles, maker
/// helpers). Used only for a gentle "Often preinstalled" hint - never to say an app is bad.</summary>
public static class OftenPreinstalledCatalog
{
    private static readonly string[] NameFragments =
    [
        "McAfee",
        "Norton",
        "WildTangent",
        "Candy Crush",
        "HP JumpStart",
        "HP Support Assistant",
        "Dell SupportAssist",
        "Dell Digital Delivery",
    ];

    public static bool IsOftenPreinstalled(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return NameFragments.Any(f => displayName.Contains(f, StringComparison.OrdinalIgnoreCase));
    }
}
