namespace Porchlight.Core.Checkup;

/// <summary>One titled block of a check-up report.</summary>
/// <param name="Title">Short heading, sentence case (e.g. "Drive space").</param>
/// <param name="Severity">The worst thing in this section.</param>
/// <param name="Lines">Plain-language lines a non-technical person can read aloud. Never put the
/// user name, file names, installed-app names, IP addresses or serial numbers in here - see
/// docs/specs/16-checkup-report.md.</param>
public sealed record CheckupSectionResult(string Title, CheckupSeverity Severity, IReadOnlyList<string> Lines);
