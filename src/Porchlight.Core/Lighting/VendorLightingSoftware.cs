namespace Porchlight.Core.Lighting;

/// <summary>
/// One vendor's RGB control software that can conflict with OpenRGB for the same device (see
/// docs/specs/05-lighting.md addendum). Process and service names are the well-known ones for each
/// vendor's current shipping software; a vendor can rename or add processes across versions, so
/// this is a best-effort catalog, not a guarantee of detecting every version.
/// </summary>
/// <param name="Id">Stable id, used as the warning id suffix (<c>"vendor-" + Id</c>) and in tests.</param>
/// <param name="DisplayName">Shown in the warning, e.g. "Logitech G HUB".</param>
/// <param name="ProcessNames">Process names (no ".exe") checked via <c>IProcessProbe</c>.</param>
/// <param name="ServiceNames">Windows service names checked via <c>IRegistryReader.ServiceExists</c>
/// - covers a vendor's background service that can be installed (and running) without its main
/// tray app currently open.</param>
public sealed record VendorLightingSoftware(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> ServiceNames)
{
    /// <summary>The vendor RGB software this detector looks for, matching the maintainer's own
    /// five-thing list (Logitech, ASUS) plus the other common ones named in the spec.</summary>
    public static readonly IReadOnlyList<VendorLightingSoftware> All =
    [
        new("lghub", "Logitech G HUB",
            ProcessNames: ["lghub", "lghub_agent", "lghub_updater"],
            ServiceNames: ["logi_lamparray_service"]),
        new("razer-synapse", "Razer Synapse",
            ProcessNames: ["RazerCentralService", "RzSDKService", "Synapse3", "Synapse4"],
            ServiceNames: ["Razer Chroma SDK Service", "RzActionSvc"]),
        new("corsair-icue", "Corsair iCUE",
            ProcessNames: ["iCUE", "iCUEDataServer"],
            ServiceNames: ["CorsairLLAccess"]),
        new("steelseries-gg", "SteelSeries GG",
            ProcessNames: ["SteelSeriesGG", "SteelSeriesGGClient"],
            ServiceNames: ["SteelSeriesGG Client Service"]),
        new("asus-armoury-crate", "ASUS Armoury Crate / Aura",
            ProcessNames: ["ArmouryCrateService", "ArmouryCrate.UserSessionHelper", "AsusAuraService", "LightingService"],
            ServiceNames: ["LightingService", "ArmouryCrate.Service", "AuraWallpaperService"]),
        new("msi-mystic-light", "MSI Mystic Light / MSI Center",
            ProcessNames: ["MysticLight", "MSI.CentralServer", "MSICentral"],
            ServiceNames: ["MSI Center Service"]),
        new("gigabyte-rgb-fusion", "Gigabyte RGB Fusion",
            ProcessNames: ["RGBFusion", "GigabyteControlCenterService"],
            ServiceNames: ["GigabyteControlCenterService"]),
        new("signalrgb", "SignalRGB",
            ProcessNames: ["SignalRgb"],
            ServiceNames: []),
        new("nzxt-cam", "NZXT CAM",
            ProcessNames: ["NZXT CAM"],
            ServiceNames: ["NZXT CAM"]),
        new("hyperx-ngenuity", "HyperX NGenuity",
            ProcessNames: ["NGENUITY"],
            ServiceNames: []),
    ];
}
