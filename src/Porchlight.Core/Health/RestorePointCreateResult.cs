namespace Porchlight.Core.Health;

/// <summary>Outcome plus the plain sentence to show.</summary>
public sealed record RestorePointCreateResult(RestorePointCreateOutcome Outcome, string Message);
