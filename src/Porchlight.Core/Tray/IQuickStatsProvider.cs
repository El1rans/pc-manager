namespace Porchlight.Core.Tray;

/// <summary>Reads <see cref="QuickStats"/> cheaply and without touching the Dashboard's own
/// performance counters. Blocking; call from a background thread.</summary>
public interface IQuickStatsProvider
{
    QuickStats Read();
}
