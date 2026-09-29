using System.Globalization;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

/// <summary>One grouped problem row.</summary>
public sealed class ProblemRowViewModel(ProblemSummary summary)
{
    public string Title { get; } = summary.Title;

    public string? Advice { get; } = summary.Advice;

    public bool HasAdvice => !string.IsNullOrEmpty(Advice);

    public string LastText { get; } =
        "Last: " + summary.LastOccurred.LocalDateTime.ToString("d MMM yyyy, h:mm tt", CultureInfo.CurrentCulture);
}
