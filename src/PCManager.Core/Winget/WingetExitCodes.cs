namespace PCManager.Core.Winget;

/// <summary>How a single package's upgrade attempt turned out; drives the Updates page's Status
/// column and its "Finished: N updated, N failed, N skipped" summary.</summary>
public enum PackageOutcome
{
    Success,
    Skipped,
    Failed,
}

/// <summary>
/// Maps a <c>winget upgrade</c> exit code to a <see cref="PackageOutcome"/> and a short,
/// user-facing message. This is the Updates-page-specific reading of winget's exit codes (an
/// unresolved upgrade is "Skipped", not an error) - see
/// <see cref="PCManager.Core.Processes.WingetExitCodes"/> for the separate, install-flavoured
/// reading <c>IComponentService</c> needs (there, the same "no applicable update"/"already
/// installed" family of codes counts as success). The numeric constant for "no applicable upgrade"
/// is reused from that type rather than redefined here.
/// </summary>
public static class WingetExitCodes
{
    /// <summary>The install itself succeeded, but the target application was running and needed to
    /// be closed first, so winget's install step could not complete.</summary>
    public const int AppInUse = unchecked((int)0x8A150101);

    /// <summary>Maps a <c>winget upgrade --id ...</c> exit code to how that package's row should be
    /// reported. Does not look at the command's output - see <see cref="MentionsRestart"/> for the
    /// separate "Updated - restart needed" refinement applied to a <see cref="PackageOutcome.Success"/>
    /// result.</summary>
    public static (PackageOutcome Outcome, string Message) Describe(int exitCode)
    {
        if (exitCode == 0)
        {
            return (PackageOutcome.Success, "Updated");
        }

        if (exitCode == Processes.WingetExitCodes.UpdateNotApplicable)
        {
            return (PackageOutcome.Skipped, "No applicable update");
        }

        if (exitCode == AppInUse)
        {
            return (PackageOutcome.Failed, "App is running - close it");
        }

        return (PackageOutcome.Failed, $"Failed (0x{unchecked((uint)exitCode):X8})");
    }

    /// <summary>True if any line of a successful upgrade's output mentions that a restart is
    /// needed to finish (winget itself has no distinct exit code for this).</summary>
    public static bool MentionsRestart(IReadOnlyList<string> outputLines)
    {
        ArgumentNullException.ThrowIfNull(outputLines);
        foreach (var line in outputLines)
        {
            if (line.Contains("restart", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
