using System.Globalization;

namespace Porchlight.Core.Processes;

/// <summary>
/// Builds the Updates page's explanation text for winget's "app in use" outcome once the actual
/// locking processes are known (or aren't) - kept as a pure static builder, separate from
/// <see cref="IAppLockDetector"/>'s real Restart Manager lookup, so the wording is unit-testable
/// with a fake list of names instead of real locked files. See
/// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum: real case, OBS Studio blocked by
/// Chrome and another app holding its virtual-camera DLL open while OBS itself was not running.
/// </summary>
public static class AppInUseExplanation
{
    /// <summary>Cap on how many process names are named individually before falling back to
    /// "and N more" - keeps the sentence readable even when many programs hold a lock.</summary>
    public const int MaxNamedProcesses = 5;

    /// <summary>
    /// "These programs are using &lt;appName&gt;'s files: A, B. Close them, then try again." when
    /// <paramref name="lockingProcessNames"/> is non-empty, or the generic
    /// <see cref="GenericExplanation"/> when it's empty (nothing found, or the lookup itself
    /// couldn't run - see <see cref="IAppInUseDiagnosticsService"/>).
    /// </summary>
    public static string Build(string appName, IReadOnlyList<string> lockingProcessNames)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        ArgumentNullException.ThrowIfNull(lockingProcessNames);

        if (lockingProcessNames.Count == 0)
        {
            return GenericExplanation;
        }

        var shown = lockingProcessNames.Count > MaxNamedProcesses
            ? lockingProcessNames.Take(MaxNamedProcesses).ToList()
            : lockingProcessNames;

        var names = string.Join(", ", shown);
        if (lockingProcessNames.Count > shown.Count)
        {
            names += string.Create(
                CultureInfo.InvariantCulture, $" and {lockingProcessNames.Count - shown.Count} more");
        }

        var sentenceStart = lockingProcessNames.Count == 1 ? "This program is" : "These programs are";
        return $"{sentenceStart} using {appName}'s files: {names}. Close them, then try again.";
    }

    /// <summary>The plain "close the app" text used when no specific locking process could be
    /// identified - the same wording <c>Porchlight.Core.Processes.WingetExitCodes</c> already uses
    /// for the "app in use" outcome, so a failed lookup falls back to exactly what was shown before
    /// this addendum.</summary>
    public const string GenericExplanation =
        "The app (or one of its files) is currently open, so it can't be updated. Close it, then try again.";
}
