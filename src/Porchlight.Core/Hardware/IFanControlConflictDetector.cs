namespace Porchlight.Core.Hardware;

/// <summary>
/// Detects known vendor fan-control software (Armoury Crate, MSI Center, iCUE, ...) running
/// alongside Porchlight (spec 04 addendum). Detection only - this never stops, kills, or otherwise
/// modifies anything it finds; it exists purely to warn the user that another tool may be fighting
/// Porchlight's own fan control over the same hardware.
/// </summary>
public interface IFanControlConflictDetector
{
    /// <summary>Display names of every known conflicting fan-control tool currently detected as
    /// running (by process and/or Windows service name), or empty if none are.</summary>
    IReadOnlyList<string> DetectConflicts();
}

/// <param name="DisplayName">Shown in the Fans tab's warning banner.</param>
/// <param name="ProcessNames">Process names (no ".exe") that count as this software running.</param>
/// <param name="ServiceNames">Windows service names that count as this software running.</param>
public sealed record KnownFanControlSoftware(
    string DisplayName,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> ServiceNames);
