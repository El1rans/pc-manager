using System.Globalization;

namespace Porchlight.Core.Health;

/// <summary>Pure rules around restore points - see <c>docs/specs/14-system-health.md</c>.</summary>
public static class RestorePointRules
{
    /// <summary>Windows' default minimum gap between restore points: 24 hours.</summary>
    public const int DefaultFrequencyMinutes = 1440;

    /// <summary>Clock slack when checking whether a just-created restore point shows up in the list.</summary>
    public static readonly TimeSpan CreationClockSlack = TimeSpan.FromMinutes(1);

    // CreateRestorePoint return codes we explain in plain words.
    public const int ErrorAccessDenied = 5;
    public const int ErrorServiceDisabled = 1058;
    public const int ErrorServiceNotStarted = 1056;
    private const int ErrorNotSupported = 50;
    private const int ErrorTooSoon = 1;

    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;

    /// <summary>When the frequency limit next allows a restore point, or null when one is allowed now.</summary>
    public static DateTimeOffset? NextAllowed(
        int frequencyMinutes, IReadOnlyList<RestorePointInfo>? recent, DateTimeOffset now)
    {
        if (frequencyMinutes <= 0 || recent is null || recent.Count == 0)
        {
            return null;
        }

        var newest = recent.Max(r => r.CreatedAt);
        var next = newest.AddMinutes(frequencyMinutes);
        return next > now ? next : null;
    }

    public static RestorePointAvailability Evaluate(RestorePointStatus status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status.ProtectionEnabled == false)
        {
            return new RestorePointAvailability(
                false, "System Protection is turned off, so Windows can't make restore points. Turn it on first.");
        }

        if (NextAllowed(status.FrequencyMinutes, status.Recent, now) is { } next)
        {
            return new RestorePointAvailability(
                false,
                $"Windows only allows a new restore point every {DescribeFrequency(status.FrequencyMinutes)}. " +
                $"You can make another in {DescribeWait(next - now)}.");
        }

        return new RestorePointAvailability(
            true, "Ready. A restore point lets you return Windows to how it is now if a change goes wrong.");
    }

    /// <summary>True when <paramref name="after"/> holds a restore point created since <paramref name="startedAt"/>.</summary>
    public static bool WasCreated(IReadOnlyList<RestorePointInfo>? after, DateTimeOffset startedAt) =>
        after is not null && after.Any(r => r.CreatedAt >= startedAt - CreationClockSlack);

    /// <summary>"24 hours", "2 hours", "90 minutes".</summary>
    public static string DescribeFrequency(int minutes)
    {
        if (minutes % MinutesPerHour == 0 && minutes >= MinutesPerHour)
        {
            var hours = minutes / MinutesPerHour;
            return string.Create(CultureInfo.InvariantCulture, $"{hours} {(hours == 1 ? "hour" : "hours")}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes");
    }

    /// <summary>"less than an hour", "about 3 hours", "about 1 day".</summary>
    public static string DescribeWait(TimeSpan wait)
    {
        var totalHours = wait.TotalHours;
        if (totalHours < 1)
        {
            return "less than an hour";
        }

        if (totalHours >= HoursPerDay)
        {
            var days = (int)Math.Round(totalHours / HoursPerDay);
            return string.Create(CultureInfo.InvariantCulture, $"about {days} {(days == 1 ? "day" : "days")}");
        }

        var rounded = (int)Math.Round(totalHours);
        return string.Create(CultureInfo.InvariantCulture, $"about {rounded} {(rounded == 1 ? "hour" : "hours")}");
    }

    /// <summary>Plain text for a non-zero <c>CreateRestorePoint</c> return value.</summary>
    public static string DescribeError(int returnValue) => returnValue switch
    {
        ErrorAccessDenied => "Windows refused because Porchlight isn't running as administrator.",
        ErrorServiceDisabled or ErrorServiceNotStarted =>
            "System Protection is turned off, so Windows can't make restore points. Turn it on first.",
        ErrorNotSupported => "This version of Windows doesn't support restore points.",
        ErrorTooSoon => "Windows couldn't make a restore point right now. Try again in a few minutes.",
        _ => "Windows couldn't make a restore point. Try again, or ask for help.",
    };
}
