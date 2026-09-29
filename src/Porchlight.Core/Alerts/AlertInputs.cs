using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Alerts;

/// <summary>One observation of the PC's health for <see cref="AlertEvaluator.Evaluate"/>.</summary>
/// <param name="Drives">Fixed drives.</param>
/// <param name="CpuTemperatureC">CPU temperature, or null when unavailable (no admin/driver).</param>
/// <param name="GpuTemperatureC">GPU temperature, or null when unavailable.</param>
/// <param name="RestartPending">Whether Windows has a restart pending.</param>
public sealed record AlertInputs(
    IReadOnlyList<DriveSnapshot> Drives,
    double? CpuTemperatureC,
    double? GpuTemperatureC,
    bool RestartPending);
