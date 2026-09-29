using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the "Free up space" feature.</summary>
public sealed class CleanupSettings
{
    /// <summary>Smallest file, in MB, listed under "Big files".</summary>
    public int LargeFileThresholdMb { get; set; } = (int)LargeFileSearchOptions.DefaultBigFileMegabytes;

    /// <summary>Categories the user has unticked (by <see cref="CleanupCategoryId"/> name). Only the
    /// user's own choices are stored, so a category that is off by default stays off until ticked.</summary>
    public List<string> DeselectedCategories { get; set; } = [];

    /// <summary>Categories the user has explicitly ticked even though they start unticked.</summary>
    public List<string> SelectedCategories { get; set; } = [];

    /// <summary>Total bytes Porchlight has freed so far.</summary>
    public long TotalBytesFreed { get; set; }

    /// <summary>When the last clean finished, or null if never.</summary>
    public DateTime? LastCleanedUtc { get; set; }
}
