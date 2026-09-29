namespace Porchlight.Core.Checkup;

/// <summary>
/// One contributor to the check-up report. Register with
/// <c>services.AddSingleton&lt;ICheckupSection, MySection&gt;()</c>; <see cref="CheckupReportBuilder"/>
/// picks up every registration, ordered by <see cref="Order"/>. Sections must be read-only and fast
/// (use cached data, never start expensive work such as a winget run) and must follow the privacy
/// rules in docs/specs/16-checkup-report.md.
/// </summary>
public interface ICheckupSection
{
    /// <summary>Heading used if this section fails and has to be replaced by a "could not check" entry.</summary>
    string Title { get; }

    /// <summary>Sort key. Built-in sections use 100, 200, 300, ...; leave gaps for other features.</summary>
    int Order { get; }

    /// <summary>Builds the section, or returns null to leave it out (nothing to report).</summary>
    Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken);
}
