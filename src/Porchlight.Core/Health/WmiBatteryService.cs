using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Health;

/// <inheritdoc cref="IBatteryService"/>
public sealed partial class WmiBatteryService(ILogger<WmiBatteryService> logger) : IBatteryService
{
    private const string WmiScope = @"root\wmi";
    private const string CimScope = @"root\cimv2";

    public async Task<HealthReadResult<BatteryReading?>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(Read, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogReadFailed(ex);
            return HealthReadResult<BatteryReading?>.Fail("Windows would not tell us about the battery.");
        }
    }

    private HealthReadResult<BatteryReading?> Read()
    {
        var packs = WmiReader.Query(
            CimScope, "SELECT * FROM Win32_Battery", ["EstimatedChargeRemaining", "BatteryStatus"]);
        if (packs.Count == 0)
        {
            return HealthReadResult<BatteryReading?>.Ok(null);
        }

        var pack = packs[0];

        var design = TryQuery("SELECT * FROM BatteryStaticData", "DesignedCapacity");
        var full = TryQuery("SELECT * FROM BatteryFullChargedCapacity", "FullChargedCapacity");
        var cycles = TryQuery("SELECT * FROM BatteryCycleCount", "CycleCount");

        return HealthReadResult<BatteryReading?>.Ok(new BatteryReading(
            design,
            full,
            cycles is { } c ? (int)Math.Min(c, int.MaxValue) : null,
            WmiReader.GetInt(pack, "EstimatedChargeRemaining"),
            BatteryHealthCalculator.StateFromWin32Status(WmiReader.GetInt(pack, "BatteryStatus"))));
    }

    /// <summary>First instance's numeric <paramref name="property"/>; null when the class or
    /// value is not available on this device (common - not every battery reports wear/cycles).</summary>
    private long? TryQuery(string query, string property)
    {
        try
        {
            var rows = WmiReader.Query(WmiScope, query, [property]);
            return rows.Count > 0 ? WmiReader.GetLong(rows[0], property) : null;
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogOptionalReadFailed(ex, query);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the battery.")]
    private partial void LogReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional battery query failed (treated as not reported): {Query}")]
    private partial void LogOptionalReadFailed(Exception ex, string query);
}
