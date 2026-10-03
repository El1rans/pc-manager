namespace Porchlight.Core.Safety;

/// <summary>One install attempt from the Windows Update history.</summary>
public sealed record UpdateHistoryEntry(DateTimeOffset Date, string Title, UpdateHistoryResult Result);
