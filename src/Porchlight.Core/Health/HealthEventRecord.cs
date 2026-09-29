namespace Porchlight.Core.Health;

/// <summary>A minimal, Windows-free copy of one Event Log record - all the problem summariser needs.</summary>
/// <param name="ProviderName">e.g. "Application Error", "Microsoft-Windows-Kernel-Power".</param>
/// <param name="EventId">The event id.</param>
/// <param name="TimeCreated">When it was logged.</param>
/// <param name="Properties">The event's data values as text, in order (e.g. the crashed app's file
/// name for "Application Error" 1000).</param>
public sealed record HealthEventRecord(
    string ProviderName, int EventId, DateTimeOffset TimeCreated, IReadOnlyList<string> Properties);
