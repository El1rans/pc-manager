#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="IDiskSpaceMapper"/> returning a made-up folder tree - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoDiskSpaceMapper : IDiskSpaceMapper
{
    private const long Mb = 1024L * 1024;
    private const long Gb = 1024L * Mb;

    public async Task<DiskNode> MapAsync(string root, IProgress<DiskMapProgress>? progress, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

        var home = new DiskNode(root, "Demo", null);
        var videos = Folder(home, "Videos", ("Family holiday 2019.mp4", 3400 * Mb), ("Birthday.mp4", 900 * Mb));
        Folder(videos, "Old clips", ("clip1.mov", 700 * Mb));
        Folder(home, "Pictures", ("Scan001.tif", 220 * Mb), ("Scan002.tif", 210 * Mb));
        Folder(home, "Documents", ("Old backup.zip", 1900 * Mb), ("Taxes.pdf", 12 * Mb));
        Folder(home, "Downloads", ("Setup-1.2.3.exe", 220 * Mb));
        var appData = Folder(home, "AppData", ("cache.db", 1 * Gb));
        appData.UnreadableFolders++;
        home.UnreadableFolders++;

        Aggregate(home);
        progress?.Report(new DiskMapProgress(root, home.TotalBytes, home.FileCount));
        return home;
    }

    private static DiskNode Folder(DiskNode parent, string name, params (string Name, long Bytes)[] files)
    {
        var node = new DiskNode(Path.Combine(parent.Path, name), name, parent);
        parent.ChildList.Add(node);
        foreach (var (fileName, bytes) in files)
        {
            node.FileList.Add(new DiskFile(Path.Combine(node.Path, fileName), fileName, bytes, DateTime.UtcNow.AddDays(-400), CanRecycle: true));
            node.TotalBytes += bytes;
            node.FileCount++;
        }

        return node;
    }

    /// <summary>Adds each folder's children into its totals, deepest first, and sorts them.</summary>
    private static void Aggregate(DiskNode node)
    {
        foreach (var child in node.Children)
        {
            Aggregate(child);
            node.TotalBytes += child.TotalBytes;
            node.FileCount += child.FileCount;
        }

        node.ChildList.Sort(static (a, b) => b.TotalBytes.CompareTo(a.TotalBytes));
    }
}
#endif
