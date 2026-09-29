namespace Porchlight.Core.Cleanup;

/// <summary>Supplies the list of cleanup categories, resolved against the current machine.</summary>
public interface ICleanupCatalog
{
    /// <summary>Builds the category list. Cheap, but re-resolves browser profile folders each call,
    /// so call it once per scan.</summary>
    IReadOnlyList<CleanupCategory> GetCategories();
}
