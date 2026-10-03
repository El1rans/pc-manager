namespace Porchlight.Core.Safety;

/// <summary>What the update history says.</summary>
/// <param name="LastInstalled">When Windows last installed an update successfully, or null if never recorded.</param>
/// <param name="RecentFailures">Failed install attempts within <see cref="UpdateHistorySummarizer.RecentWindow"/>.</param>
public sealed record UpdateHistorySummary(DateTimeOffset? LastInstalled, int RecentFailures);
