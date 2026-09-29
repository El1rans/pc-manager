namespace Porchlight.Core.Health;

/// <summary>
/// Runs the Windows file checks. Both take 10-30 minutes and <b>cannot be cancelled once started</b>
/// (killing SFC/DISM mid-write can damage the component store): the token is honoured only before
/// the tool launches. Requires administrator rights.
/// </summary>
public interface IWindowsRepairService
{
    /// <summary>Runs <c>sfc /scannow</c>.</summary>
    Task<SfcOutcome> RunSfcAsync(IProgress<RepairProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Runs <c>DISM /Online /Cleanup-Image /RestoreHealth</c>.</summary>
    Task<DismOutcome> RunDismAsync(IProgress<RepairProgress>? progress, CancellationToken cancellationToken);
}
