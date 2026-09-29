namespace Porchlight.Core.Cleanup;

/// <summary>A progress update from a scan or clean: which category is being worked on and how much
/// has been counted (scan, per category) or freed (clean, running total) so far.</summary>
public sealed record CleanupProgress(CleanupCategoryId Category, long Bytes, int Files);
