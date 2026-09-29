using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Health;

/// <inheritdoc cref="IDiskHealthService"/>
public sealed partial class WmiDiskHealthService(ILogger<WmiDiskHealthService> logger) : IDiskHealthService
{
    private const string StorageScope = @"root\Microsoft\Windows\Storage";
    private const string WmiScope = @"root\wmi";

    private static readonly string[] PhysicalDiskProperties =
        ["DeviceId", "FriendlyName", "MediaType", "Size", "HealthStatus", "OperationalStatus"];

    private static readonly string[] ReliabilityProperties =
        ["DeviceId", "Temperature", "Wear", "ReadErrorsUncorrected"];

    private static readonly string[] PredictProperties = ["PredictFailure"];

    public async Task<HealthReadResult<DiskHealthSnapshot>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(Read, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogReadFailed(ex);
            return HealthReadResult<DiskHealthSnapshot>.Fail("Windows would not tell us about your drives.");
        }
    }

    private HealthReadResult<DiskHealthSnapshot> Read()
    {
        var disks = WmiReader.Query(StorageScope, "SELECT * FROM MSFT_PhysicalDisk", PhysicalDiskProperties);

        // Reliability counters and failure prediction are optional extras: they often need admin or
        // are not exposed by a USB enclosure, so a failure here only means "not reported".
        var reliability = TryQuery(StorageScope, "SELECT * FROM MSFT_StorageReliabilityCounter", ReliabilityProperties);
        var predict = TryQuery(WmiScope, "SELECT * FROM MSStorageDriver_FailurePredictStatus", PredictProperties);

        var anyPredicted = predict.Any(p => WmiReader.GetBool(p, "PredictFailure") == true);
        var attributePredictToDisk = disks.Count == 1;

        var reports = new List<DiskHealthReport>(disks.Count);
        foreach (var disk in disks)
        {
            var deviceId = WmiReader.GetString(disk, "DeviceId");
            var counter = reliability.FirstOrDefault(r =>
                deviceId is not null && string.Equals(WmiReader.GetString(r, "DeviceId"), deviceId, StringComparison.Ordinal));

            var info = new DiskHealthInfo(
                WmiReader.GetString(disk, "FriendlyName") ?? "Unnamed drive",
                WmiReader.GetInt(disk, "MediaType"),
                WmiReader.GetLong(disk, "Size") ?? 0,
                WmiReader.GetInt(disk, "HealthStatus"),
                WmiReader.GetIntArray(disk, "OperationalStatus"),
                counter is null ? null : WmiReader.GetInt(counter, "Temperature"),
                counter is null ? null : WmiReader.GetInt(counter, "Wear"),
                counter is null ? null : WmiReader.GetLong(counter, "ReadErrorsUncorrected"),
                attributePredictToDisk ? anyPredicted : null);

            reports.Add(new DiskHealthReport(
                info.FriendlyName,
                DiskHealthEvaluator.DescribeMediaType(info.MediaType),
                info.SizeBytes,
                DiskHealthEvaluator.Evaluate(info),
                info.TemperatureCelsius,
                info.WearPercent));
        }

        return HealthReadResult<DiskHealthSnapshot>.Ok(
            new DiskHealthSnapshot(reports, anyPredicted && !attributePredictToDisk));
    }

    private List<Dictionary<string, object?>> TryQuery(string scope, string query, IReadOnlyList<string> properties)
    {
        try
        {
            return WmiReader.Query(scope, query, properties);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogOptionalReadFailed(ex, query);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read physical disk health.")]
    private partial void LogReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional disk query failed (treated as not reported): {Query}")]
    private partial void LogOptionalReadFailed(Exception ex, string query);
}
