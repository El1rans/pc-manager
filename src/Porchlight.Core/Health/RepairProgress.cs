namespace Porchlight.Core.Health;

/// <summary>One progress update from an SFC or DISM run.</summary>
/// <param name="Percent">0-100 when the tool reported a percentage; otherwise null (keep the last one).</param>
/// <param name="Line">A cleaned, human-readable output line to append to the log; null for a pure
/// progress-bar redraw.</param>
public sealed record RepairProgress(int? Percent, string? Line);
