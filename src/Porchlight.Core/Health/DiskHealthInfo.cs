namespace Porchlight.Core.Health;

/// <summary>Raw reading for one physical disk, as gathered from WMI. Every field that Windows may
/// not report (no admin, USB enclosure, older drive) is nullable - a missing value is "not
/// reported", never a problem by itself.</summary>
/// <param name="FriendlyName">Model name, e.g. "Samsung SSD 970 EVO".</param>
/// <param name="MediaType"><c>MSFT_PhysicalDisk.MediaType</c>: 3 = HDD, 4 = SSD, 5 = SCM, 0 = unspecified.</param>
/// <param name="SizeBytes">Disk size in bytes.</param>
/// <param name="HealthStatus"><c>MSFT_PhysicalDisk.HealthStatus</c>: 0 = Healthy, 1 = Warning,
/// 2 = Unhealthy, 5 = Unknown.</param>
/// <param name="OperationalStatus"><c>MSFT_PhysicalDisk.OperationalStatus</c> codes (2 = OK, 3 = Degraded,
/// 5 = Predictive Failure, 6 = Error, 12 = No Contact, 13 = Lost Communication, ...).</param>
/// <param name="TemperatureCelsius">Reliability counter temperature.</param>
/// <param name="WearPercent">Reliability counter wear: percent of the drive's rated life used.</param>
/// <param name="ReadErrorsUncorrected">Reliability counter uncorrected read errors.</param>
/// <param name="PredictFailure"><c>MSStorageDriver_FailurePredictStatus.PredictFailure</c> when it
/// could be attributed to this disk.</param>
public sealed record DiskHealthInfo(
    string FriendlyName,
    int? MediaType,
    long SizeBytes,
    int? HealthStatus,
    IReadOnlyList<int> OperationalStatus,
    int? TemperatureCelsius = null,
    int? WearPercent = null,
    long? ReadErrorsUncorrected = null,
    bool? PredictFailure = null);
