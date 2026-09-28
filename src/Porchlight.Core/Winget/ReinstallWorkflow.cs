using Porchlight.Core.Processes;

namespace Porchlight.Core.Winget;

/// <summary>
/// Pure orchestration for the Updates page's "Reinstall..." action: uninstall the package, then -
/// only if that succeeded - install the newest version. Used when winget reports
/// <see cref="WingetOutcomeKind.ReinstallRequired"/> (install technology mismatch) for a plain
/// upgrade. See <c>docs/specs/09-friendly-update-outcomes.md</c>.
/// </summary>
/// <remarks>
/// Never retries or loops on its own - a failure at either step is reported back for the user to
/// explicitly retry (see <see cref="ReinstallOutcome"/>). <paramref name="cancellationToken"/> in
/// <see cref="RunAsync"/> is only checked before anything starts; once the uninstall or install has
/// actually launched, this class always waits for it with <see cref="CancellationToken.None"/> -
/// killing winget mid-uninstall or mid-install can leave the package in a worse state than either
/// outcome this workflow reports.
/// </remarks>
public sealed class ReinstallWorkflow
{
    private readonly IWingetClient _wingetClient;

    public ReinstallWorkflow(IWingetClient wingetClient)
    {
        _wingetClient = wingetClient;
    }

    /// <summary>
    /// Runs <c>winget uninstall</c> then, only on success, <c>winget install</c> for
    /// <paramref name="id"/>. <paramref name="log"/> receives every output line from both steps
    /// (prefixed with the command that produced it); <paramref name="progress"/> receives only the
    /// install step's redrawn progress text (the uninstall step has none worth showing).
    /// </summary>
    public async Task<ReinstallOutcome> RunAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        // Only checked before anything starts - see the type's remarks. Everything below always
        // runs to completion with CancellationToken.None.
        cancellationToken.ThrowIfCancellationRequested();

        var uninstallResult = await _wingetClient
            .UninstallAsync(id, silent, log, CancellationToken.None)
            .ConfigureAwait(false);
        var uninstallOutcome = WingetExitCodes.DescribeOutcome(uninstallResult.ExitCode, uninstallResult.Lines);

        if (uninstallResult.ExitCode != 0)
        {
            // Stop here: the app is still installed exactly as it was, nothing lost - see
            // ReinstallOutcomeKind.UninstallFailed.
            return new ReinstallOutcome(ReinstallOutcomeKind.UninstallFailed, uninstallOutcome, InstallOutcome: null);
        }

        var installResult = await _wingetClient
            .InstallAsync(id, silent, log, progress, CancellationToken.None)
            .ConfigureAwait(false);
        var installOutcome = WingetExitCodes.DescribeOutcome(installResult.ExitCode, installResult.Lines);

        var kind = installOutcome.Kind is WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded
            ? ReinstallOutcomeKind.Reinstalled
            : ReinstallOutcomeKind.InstallFailedAfterUninstall;

        return new ReinstallOutcome(kind, uninstallOutcome, installOutcome);
    }
}
