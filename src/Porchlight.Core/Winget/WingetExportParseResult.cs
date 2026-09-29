namespace Porchlight.Core.Winget;

/// <summary>Outcome of validating a <c>winget export</c> file - see <see cref="WingetExportParser"/>.</summary>
/// <param name="Apps">The usable apps (empty on failure).</param>
/// <param name="SkippedCount">Entries dropped because their id was not a plausible package id.</param>
/// <param name="Error">Plain-language reason the file was rejected; null on success.</param>
public sealed record WingetExportParseResult(IReadOnlyList<WingetExportApp> Apps, int SkippedCount, string? Error)
{
    public bool IsSuccess => Error is null;

    public static WingetExportParseResult Failure(string error) => new([], 0, error);
}
