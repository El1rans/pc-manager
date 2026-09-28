using System.Globalization;

namespace Porchlight.Core.Winget;

/// <summary>
/// A plain-language description of how a single winget operation (upgrade, uninstall, or install)
/// turned out, for the Updates page's Status column. Built by
/// <see cref="Processes.WingetExitCodes.DescribeOutcome"/> - never constructed with a made-up
/// message directly. See <c>docs/specs/09-friendly-update-outcomes.md</c>: "Every winget outcome on
/// the Updates page is a short plain-language sentence, never a bare hex code. The code stays in a
/// tooltip and in the log."
/// </summary>
/// <param name="Kind">Broad category, driving icon/color and which action to offer.</param>
/// <param name="Title">Short label for the Status column, e.g. "Needs a reinstall".</param>
/// <param name="Explanation">One or two plain sentences explaining what happened, for the tooltip
/// and the log - never shown as the only thing in the Status column.</param>
/// <param name="ExitCode">The raw winget exit code this was built from, kept for the tooltip and
/// the log; never shown to the user on its own.</param>
/// <param name="SuggestedAction">What the Updates page should offer the user to do next.</param>
public sealed record WingetOutcome(
    WingetOutcomeKind Kind,
    string Title,
    string Explanation,
    int ExitCode,
    WingetSuggestedAction SuggestedAction)
{
    /// <summary>The exit code formatted as winget itself prints it, e.g. <c>0x8A15008E</c>.</summary>
    public string ExitCodeHex => "0x" + unchecked((uint)ExitCode).ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>
    /// <see cref="Explanation"/> plus the exit code, for the Status column's tooltip and
    /// <c>AutomationProperties.HelpText</c> - see <c>docs/specs/09-friendly-update-outcomes.md</c>:
    /// "Tooltip = Explanation + \"(winget code 0x...)\"".
    /// </summary>
    public string TooltipText => $"{Explanation} (winget code {ExitCodeHex})";
}
