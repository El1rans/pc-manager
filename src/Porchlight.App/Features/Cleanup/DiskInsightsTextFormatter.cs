using System.Globalization;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Cleanup;

/// <summary>Plain-language text for the disk space map and the duplicate finder, kept pure so it is
/// unit-testable.</summary>
public static class DiskInsightsTextFormatter
{
    /// <summary>"38%", or "Under 1%" for a sliver.</summary>
    public static string FormatPercent(double percent) =>
        percent < 1 ? "Under 1%" : Math.Round(percent).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>"12.4 GB in 48,210 files".</summary>
    public static string FormatSummary(long bytes, long files) =>
        $"{ByteFormatter.FormatBytes(bytes)} in {files.ToString("N0", CultureInfo.InvariantCulture)} {(files == 1 ? "file" : "files")}";

    /// <summary>Empty when everything could be read.</summary>
    public static string FormatCouldntRead(int folders)
    {
        if (folders <= 0)
        {
            return string.Empty;
        }

        var noun = folders == 1 ? "folder" : "folders";
        return $"Couldn't read {folders.ToString("N0", CultureInfo.InvariantCulture)} {noun} (Windows protects them). Their size is not included.";
    }

    /// <summary>"Counted 12.4 GB in 48,210 files..."</summary>
    public static string FormatMapProgress(DiskMapProgress progress) =>
        $"Counted {FormatSummary(progress.Bytes, progress.Files)}...";

    /// <summary>What the finder is doing right now.</summary>
    public static string FormatDuplicateProgress(DuplicateProgress progress) => progress.Stage switch
    {
        DuplicateStage.Listing =>
            $"Looking through your files... {progress.Done.ToString("N0", CultureInfo.InvariantCulture)} big files so far",
        DuplicateStage.QuickCheck =>
            $"Comparing files of the same size... {progress.Done.ToString("N0", CultureInfo.InvariantCulture)} of {progress.Total.ToString("N0", CultureInfo.InvariantCulture)}",
        _ =>
            $"Confirming exact matches... {progress.Done.ToString("N0", CultureInfo.InvariantCulture)} of {progress.Total.ToString("N0", CultureInfo.InvariantCulture)}",
    };

    /// <summary>"3 copies of Holiday.mp4".</summary>
    public static string FormatGroupTitle(DuplicateGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return $"{group.Files.Count} copies of {DuplicateSelection.PickNewest(group).Name}";
    }

    /// <summary>"1.2 GB could be freed".</summary>
    public static string FormatWasted(long bytes) => $"{ByteFormatter.FormatBytes(bytes)} could be freed";

    /// <summary>"Moved 4 files (2.3 GB) to the Recycle Bin. 1 file was left alone."</summary>
    public static string FormatRemoveResult(DuplicateRemoveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var files = result.FilesRemoved == 1 ? "file" : "files";
        var parts = new List<string>
        {
            result.WasCancelled
                ? $"Stopped. Moved {result.FilesRemoved} {files} ({ByteFormatter.FormatBytes(result.BytesFreed)}) to the Recycle Bin."
                : $"Moved {result.FilesRemoved} {files} ({ByteFormatter.FormatBytes(result.BytesFreed)}) to the Recycle Bin.",
        };

        var leftAlone = result.FilesFailed + result.FilesChanged;
        if (leftAlone > 0)
        {
            parts.Add($"{leftAlone} {(leftAlone == 1 ? "file was" : "files were")} left alone because {(leftAlone == 1 ? "it was" : "they were")} in use or had changed.");
        }

        if (result.GroupsKeptOne > 0)
        {
            parts.Add("One copy of each file was kept.");
        }

        return string.Join(' ', parts);
    }
}
