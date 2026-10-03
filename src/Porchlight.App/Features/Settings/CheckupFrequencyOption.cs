using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>One choice in the check-up reminder frequency picker.</summary>
public sealed record CheckupFrequencyOption(CheckupReminderFrequency Value, string Label);
