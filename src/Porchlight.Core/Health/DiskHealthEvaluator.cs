using System.Globalization;

namespace Porchlight.Core.Health;

/// <summary>Pure verdict logic for a physical disk - see <c>docs/specs/14-system-health.md</c>.</summary>
public static class DiskHealthEvaluator
{
    /// <summary>Text for <see cref="DiskHealthVerdict.Healthy"/>.</summary>
    public const string HealthyText = "Healthy";

    /// <summary>Text for <see cref="DiskHealthVerdict.Warning"/>.</summary>
    public const string WarningText = "Warning - back up your files soon";

    /// <summary>Text for <see cref="DiskHealthVerdict.Unknown"/>.</summary>
    public const string UnknownText = "Unknown";

    /// <summary>Wear (percent of rated life used) at or above which the drive is flagged.</summary>
    public const int WearWarningPercent = 90;

    /// <summary>Temperature (degrees C) at or above which the drive is flagged as running hot.</summary>
    public const int HotCelsius = 70;

    private const int HealthStatusHealthy = 0;
    private const int HealthStatusWarning = 1;
    private const int HealthStatusUnhealthy = 2;

    private const int MediaTypeHdd = 3;
    private const int MediaTypeSsd = 4;
    private const int MediaTypeScm = 5;

    // MSFT_PhysicalDisk.OperationalStatus values that mean "not fine".
    private const int OperationalDegraded = 3;
    private const int OperationalPredictiveFailure = 5;
    private const int OperationalError = 6;
    private const int OperationalNonRecoverableError = 7;
    private const int OperationalNoContact = 12;
    private const int OperationalLostCommunication = 13;

    private static readonly int[] BadOperationalStatuses =
    [
        OperationalDegraded, OperationalPredictiveFailure, OperationalError,
        OperationalNonRecoverableError, OperationalNoContact, OperationalLostCommunication,
    ];

    public static DiskHealthAssessment Evaluate(DiskHealthInfo disk)
    {
        ArgumentNullException.ThrowIfNull(disk);

        var reasons = new List<string>();

        if (disk.PredictFailure == true)
        {
            reasons.Add("Windows expects this drive to fail.");
        }

        if (disk.HealthStatus is HealthStatusWarning)
        {
            reasons.Add("Windows reports this drive as having a problem.");
        }
        else if (disk.HealthStatus is HealthStatusUnhealthy)
        {
            reasons.Add("Windows reports this drive as unhealthy.");
        }

        if (disk.OperationalStatus.Any(s => BadOperationalStatuses.Contains(s)))
        {
            reasons.Add("This drive is not working normally.");
        }

        if (disk.WearPercent is { } wear && wear >= WearWarningPercent)
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"This drive has used {wear}% of its life."));
        }

        if (disk.ReadErrorsUncorrected is > 0)
        {
            reasons.Add("This drive has had read errors it could not fix.");
        }

        if (disk.TemperatureCelsius is { } temp && temp >= HotCelsius)
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"This drive is running hot ({temp} °C)."));
        }

        if (reasons.Count > 0)
        {
            return new DiskHealthAssessment(DiskHealthVerdict.Warning, WarningText, reasons);
        }

        return disk.HealthStatus is HealthStatusHealthy
            ? new DiskHealthAssessment(DiskHealthVerdict.Healthy, HealthyText, [])
            : new DiskHealthAssessment(DiskHealthVerdict.Unknown, UnknownText, []);
    }

    /// <summary>"SSD", "Hard disk", "Storage-class memory" or "Drive" for an unspecified type.</summary>
    public static string DescribeMediaType(int? mediaType) => mediaType switch
    {
        MediaTypeSsd => "SSD",
        MediaTypeHdd => "Hard disk",
        MediaTypeScm => "Storage-class memory",
        _ => "Drive",
    };
}
