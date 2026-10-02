namespace Porchlight.Core.Winget;

/// <summary>
/// One line of the Updates page's history: a single update, reinstall or install Porchlight ran
/// through winget, successful or not. A plain mutable POCO (like <see cref="PersistedUpdateOutcome"/>)
/// so it round-trips through JSON predictably. See <c>docs/specs/26-update-history.md</c>.
/// </summary>
public sealed class UpdateHistoryEntry
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string PackageId { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    /// <summary>The version that was installed before, or null when it was unknown.</summary>
    public string? FromVersion { get; set; }

    /// <summary>The version that was being installed, or null when unknown.</summary>
    public string? ToVersion { get; set; }

    public UpdateHistoryAction Action { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>The friendly outcome title from spec 09 (for example "Needs a reinstall").</summary>
    public string OutcomeTitle { get; set; } = string.Empty;

    /// <summary>The friendly explanation the live row showed; empty on success.</summary>
    public string Explanation { get; set; } = string.Empty;

    public int ExitCode { get; set; }
}
