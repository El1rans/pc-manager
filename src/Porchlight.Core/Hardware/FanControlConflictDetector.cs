using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Hardware;

/// <inheritdoc cref="IFanControlConflictDetector"/>
/// <remarks>
/// Pure decision logic over <see cref="IRunningSoftwareLister"/>'s snapshot of what is currently
/// running - fully unit testable with a fake lister, matching the rest of the Hardware feature's
/// pattern of keeping decisions free of any direct OS call. Never calls anything that could stop,
/// kill, disable, or otherwise touch a detected process/service - see <see cref="KnownSoftware"/>'s
/// entries, which are read-only match criteria, not actions.
/// </remarks>
public sealed partial class FanControlConflictDetector : IFanControlConflictDetector
{
    /// <summary>Known vendor fan-control tools that write to the same SuperIO/EC/GPU fan channels
    /// Porchlight does, and so can fight its software control over them (spec 04 addendum).</summary>
    public static IReadOnlyList<KnownFanControlSoftware> KnownSoftware { get; } =
    [
        new("ASUS Fan Control / Armoury Crate",
            ProcessNames: ["AsusFanControlService", "ArmouryCrate.Service", "ArmouryCrate.UserSessionHelper"],
            ServiceNames: ["AsusFanControlService", "ArmouryCrate.Service"]),
        new("MSI Center / Dragon Center",
            ProcessNames: ["MSI Center", "MSI.Center", "Dragon Center", "DragonCenter"],
            ServiceNames: ["MSI Center Service", "Dragon Center Service"]),
        new("Gigabyte SIV / Control Center",
            ProcessNames: ["SIV", "GCC", "GigabyteControlCenter"],
            ServiceNames: ["GigabyteControlCenterService"]),
        new("FanControl",
            ProcessNames: ["FanControl"],
            ServiceNames: []),
        new("SpeedFan",
            ProcessNames: ["speedfan"],
            ServiceNames: []),
        new("Argus Monitor",
            ProcessNames: ["ArgusMonitor"],
            ServiceNames: ["ArgusMonitorService"]),
        new("iCUE (Corsair Commander)",
            ProcessNames: ["iCUE"],
            ServiceNames: ["CorsairService", "CorsairServiceService"]),
        new("NZXT CAM",
            ProcessNames: ["NZXT CAM", "CAM"],
            ServiceNames: ["NZXT CAM Service"]),
        new("Lian Li L-Connect",
            ProcessNames: ["L-Connect", "LConnect3"],
            ServiceNames: ["L-Connect Service"]),
    ];

    private readonly IRunningSoftwareLister _lister;
    private readonly ILogger<FanControlConflictDetector> _logger;

    public FanControlConflictDetector(IRunningSoftwareLister lister, ILogger<FanControlConflictDetector> logger)
    {
        _lister = lister;
        _logger = logger;
    }

    public IReadOnlyList<string> DetectConflicts()
    {
        var runningProcesses = _lister.GetRunningProcessNames();
        var runningServices = _lister.GetRunningServiceNames();

        var detected = new List<string>();
        foreach (var software in KnownSoftware)
        {
            var matched = software.ProcessNames.Any(name => runningProcesses.Contains(name, StringComparer.OrdinalIgnoreCase))
                || software.ServiceNames.Any(name => runningServices.Contains(name, StringComparer.OrdinalIgnoreCase));

            if (!matched)
            {
                continue;
            }

            detected.Add(software.DisplayName);
            LogDetected(software.DisplayName);
        }

        return detected;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Detected conflicting fan-control software: {Software}.")]
    private partial void LogDetected(string software);
}
