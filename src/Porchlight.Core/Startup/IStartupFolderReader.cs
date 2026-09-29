namespace Porchlight.Core.Startup;

/// <summary>Lists the files in a Startup folder.</summary>
public interface IStartupFolderReader
{
    /// <summary>Items of the folder for <paramref name="source"/> (empty for a registry source or a missing folder).</summary>
    IReadOnlyList<StartupFolderItem> ReadFolder(StartupSource source);
}
