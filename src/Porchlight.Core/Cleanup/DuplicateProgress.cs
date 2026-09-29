namespace Porchlight.Core.Cleanup;

/// <summary>A progress update from <see cref="IDuplicateFinder"/>. <paramref name="Total"/> is 0 while
/// the total is still unknown (the listing stage).</summary>
public sealed record DuplicateProgress(DuplicateStage Stage, int Done, int Total);
