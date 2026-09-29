namespace Porchlight.Core.Health;

/// <summary>One existing restore point (read-only).</summary>
/// <param name="SequenceNumber">Windows' sequence number.</param>
/// <param name="Description">The name shown in System Restore.</param>
/// <param name="CreatedAt">When it was created.</param>
public sealed record RestorePointInfo(int SequenceNumber, string Description, DateTimeOffset CreatedAt);
