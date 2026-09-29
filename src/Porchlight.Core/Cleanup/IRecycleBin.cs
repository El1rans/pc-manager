namespace Porchlight.Core.Cleanup;

/// <summary>The Windows Recycle Bin, behind an interface so cleanup tests never touch the real one.</summary>
public interface IRecycleBin
{
    /// <summary>Total size and item count of everything in the Recycle Bin (all drives).</summary>
    RecycleBinInfo QuerySize();

    /// <summary>Permanently empties the Recycle Bin without any Windows prompt or sound. Returns
    /// false if Windows reported a failure (for example, it was already empty).</summary>
    bool Empty();
}
