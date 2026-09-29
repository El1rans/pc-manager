namespace Porchlight.App.Features.Updates;

/// <summary>Typed outcome of an update check. <see cref="Count"/> is the number of non-ignored
/// updates and is meaningful only when <see cref="Status"/> is <see cref="UpdateCheckStatus.Succeeded"/>.</summary>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, int Count)
{
    public static UpdateCheckResult Failed { get; } = new(UpdateCheckStatus.Failed, 0);

    public static UpdateCheckResult Skipped { get; } = new(UpdateCheckStatus.Skipped, 0);

    public static UpdateCheckResult Succeeded(int count) => new(UpdateCheckStatus.Succeeded, count);
}
