namespace Porchlight.App.Features.Updates;

/// <summary>One day's worth of history rows ("Today", "Yesterday", "Monday, 28 September").</summary>
public sealed record UpdateHistoryGroupViewModel(string Label, IReadOnlyList<UpdateHistoryEntryViewModel> Entries);
