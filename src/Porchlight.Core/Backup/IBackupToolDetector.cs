namespace Porchlight.Core.Backup;

/// <summary>Finds well-known backup programs in the installed-apps list.</summary>
public interface IBackupToolDetector
{
    /// <summary>Friendly names of backup tools found, de-duplicated and sorted; call off the UI thread.</summary>
    IReadOnlyList<string> Find();
}
