using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>One choice in the "check for app updates" picker.</summary>
public sealed record ScheduleOption(UpdateCheckSchedule Value, string Label);
