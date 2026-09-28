namespace Porchlight.Core.Lighting;

/// <summary>Outcome of applying a color to more than one device at once (<c>SetAllColorAsync</c>,
/// <c>TurnOffAllAsync</c>): a device-specific failure (e.g. an unusual mode this device reports)
/// does not abort the rest, so the caller gets a count of each instead of a single bool.</summary>
/// <param name="SucceededCount">How many devices were set successfully.</param>
/// <param name="FailedCount">How many devices failed (and were skipped, logged, but did not stop
/// the remaining devices from being tried).</param>
public sealed record LightingApplyResult(int SucceededCount, int FailedCount)
{
    public static readonly LightingApplyResult NotConnected = new(0, 0);

    public bool AllSucceeded => FailedCount == 0 && SucceededCount > 0;

    public bool AnyFailed => FailedCount > 0;
}
