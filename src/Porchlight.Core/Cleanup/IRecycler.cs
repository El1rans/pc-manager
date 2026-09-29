namespace Porchlight.Core.Cleanup;

/// <summary>Sends a single file to the Recycle Bin, so it can always be restored. This is the only
/// way the "Free up space" page removes a personal file.</summary>
public interface IRecycler
{
    /// <summary>Moves <paramref name="path"/> to the Recycle Bin without any Windows prompt. Returns
    /// true on success. Never permanently deletes.</summary>
    bool MoveToRecycleBin(string path);
}
