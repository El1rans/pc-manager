namespace Porchlight.Core.Health;

/// <summary>Plain result of judging a battery.</summary>
/// <param name="HealthPercent">Full-charge capacity as a percent of design capacity, 0-100; null if unknown.</param>
/// <param name="Verdict">Overall verdict.</param>
/// <param name="Text">One plain sentence, e.g. "Worn - holds about 60% of its original charge".</param>
public sealed record BatteryAssessment(int? HealthPercent, BatteryVerdict Verdict, string Text);
