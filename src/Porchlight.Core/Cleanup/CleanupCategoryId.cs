namespace Porchlight.Core.Cleanup;

/// <summary>Stable identifiers for the cleanup categories, shared by the catalog, settings (the
/// user's remembered "unticked" choices are stored by name) and the UI.</summary>
public enum CleanupCategoryId
{
    TemporaryFiles,
    WindowsTemporaryFiles,
    BrowserCaches,
    ThumbnailCache,
    CrashReports,
    WindowsUpdateLeftovers,
    DeliveryOptimizationCache,
    RecycleBin,
}
