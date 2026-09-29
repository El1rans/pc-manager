namespace Porchlight.Core.Cleanup;

/// <summary>Which entries the personal-file scanners (big files, duplicates, disk map) must not
/// treat as ordinary local files.</summary>
internal static class PersonalFileRules
{
    public const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    public const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    /// <summary>Cloud-only placeholders: reading their size would not free local space.</summary>
    public const FileAttributes CloudPlaceholder = FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess;

    /// <summary>Reparse points, hidden/system entries and cloud-only placeholders.</summary>
    public const FileAttributes Skipped =
        FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System | CloudPlaceholder;

    public static bool IsSkipped(CleanupEntry entry) => (entry.Attributes & Skipped) != 0;

    public static bool IsCloudPlaceholder(CleanupEntry entry) => (entry.Attributes & CloudPlaceholder) != 0;
}
