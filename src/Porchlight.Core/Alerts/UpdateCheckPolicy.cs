using Porchlight.Core.Settings;

namespace Porchlight.Core.Alerts;

/// <summary>Decides when a scheduled update check is due.</summary>
public static class UpdateCheckPolicy
{
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan WeeklyInterval = TimeSpan.FromDays(7);

    public static bool IsDue(UpdateCheckSchedule schedule, DateTimeOffset? lastCheckUtc, DateTimeOffset now)
    {
        var interval = schedule switch
        {
            UpdateCheckSchedule.Daily => DailyInterval,
            UpdateCheckSchedule.Weekly => WeeklyInterval,
            _ => (TimeSpan?)null,
        };

        if (interval is null)
        {
            return false;
        }

        // A clock that moved backwards (last check "in the future") counts as due rather than
        // silencing checks until the clock catches up.
        return lastCheckUtc is null || lastCheckUtc > now || now - lastCheckUtc >= interval;
    }
}
