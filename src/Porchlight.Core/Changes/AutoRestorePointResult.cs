namespace Porchlight.Core.Changes;

/// <summary>Outcome plus an optional quiet sentence to show beside the change (null = say nothing).</summary>
public sealed record AutoRestorePointResult(AutoRestorePointOutcome Outcome, string? Note)
{
    public bool Created => Outcome == AutoRestorePointOutcome.Created;
}
