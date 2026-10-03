namespace Porchlight.Core.Backup;

/// <summary>The verdict plus the plain-language text and button offers that go with it.</summary>
/// <param name="Verdict">Overall answer.</param>
/// <param name="Headline">One sentence, e.g. "Nothing is backing up your files."</param>
/// <param name="Lines">Per-source detail lines.</param>
/// <param name="Nudge">What to do next, or null when nothing is needed.</param>
/// <param name="OfferFileHistory">Show "Turn on File History".</param>
/// <param name="OtherToolsNote">"Also found: ..." text, or null when none were found.</param>
public sealed record BackupAssessment(
    BackupVerdict Verdict,
    string Headline,
    IReadOnlyList<string> Lines,
    string? Nudge,
    bool OfferFileHistory,
    string? OtherToolsNote);
