namespace Porchlight.Core.WebConsole;

/// <summary>Supplies the stats the web console serves. Read-only by design: implementations only
/// observe the PC and never change anything on it.</summary>
public interface IWebConsoleStatsSource
{
    /// <summary>The current stats. Safe to call from several threads at once; calls closer together
    /// than <see cref="WebConsoleOptions.MinSampleInterval"/> share one sample.</summary>
    Task<WebConsoleStats> GetAsync(CancellationToken cancellationToken);
}
