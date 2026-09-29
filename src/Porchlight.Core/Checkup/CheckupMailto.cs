using System.Text;
using System.Text.RegularExpressions;

namespace Porchlight.Core.Checkup;

/// <summary>Builds the <c>mailto:</c> address that opens the user's default mail client with the
/// check-up in the body. Porchlight never sends the email itself.</summary>
public static partial class CheckupMailto
{
    /// <summary>Longest URI produced. Common Windows mail handlers start failing around 2000
    /// characters, so stay comfortably below.</summary>
    public const int MaxUriLength = 1800;

    private const int MaxEmailLength = 254;
    private const string LineBreak = "\r\n";
    private const string TruncationNote = "[Report shortened. Use Save report... in Porchlight for the full check-up.]";

    /// <summary>True when <paramref name="email"/> is a plain single address with no characters that
    /// could alter the URI or add mail headers.</summary>
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= MaxEmailLength && EmailPattern().IsMatch(email.Trim());

    /// <summary>Builds the URI. An invalid or empty <paramref name="helperEmail"/> is left out
    /// (the message opens with no recipient). A body that does not fit is cut at a line boundary and
    /// ends with a note that the full report can be saved.</summary>
    public static string Build(string? helperEmail, string subject, string body)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);

        var recipient = IsValidEmail(helperEmail) ? helperEmail!.Trim() : string.Empty;
        var prefix = $"mailto:{recipient}?subject={Uri.EscapeDataString(subject)}&body=";
        var budget = MaxUriLength - prefix.Length;

        var lines = body.ReplaceLineEndings("\n").Split('\n');
        var full = Uri.EscapeDataString(string.Join(LineBreak, lines));
        if (full.Length <= budget)
        {
            return prefix + full;
        }

        var used = Uri.EscapeDataString(LineBreak + LineBreak + TruncationNote).Length;
        var kept = new StringBuilder();
        foreach (var line in lines)
        {
            var separator = kept.Length == 0 ? string.Empty : LineBreak;
            var cost = Uri.EscapeDataString(separator + line).Length;
            if (used + cost > budget)
            {
                break;
            }

            kept.Append(separator).Append(line);
            used += cost;
        }

        return prefix + Uri.EscapeDataString(kept + LineBreak + LineBreak + TruncationNote);
    }

    [GeneratedRegex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
