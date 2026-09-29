using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Cleanup;

/// <summary>One line of the disk map drill-down list: a folder, a file, or the "everything else"
/// remainder.</summary>
public sealed class DiskRowViewModel
{
    private const string FolderGlyph = "";
    private const string FileGlyph = "";
    private const string OtherGlyph = "";

    private DiskRowViewModel(string name, string kind, string glyph, long bytes, long parentBytes)
    {
        Name = name;
        KindText = kind;
        Glyph = glyph;
        SizeText = ByteFormatter.FormatBytes(bytes);
        Percent = parentBytes <= 0 ? 0 : Math.Clamp(100.0 * bytes / parentBytes, 0, 100);
        PercentText = DiskInsightsTextFormatter.FormatPercent(Percent);
        AutomationName = $"{name}, {SizeText}, {PercentText}";
    }

    public string Name { get; }

    /// <summary>"Folder", "File" or "Other files" - shown next to the icon so the type is never
    /// conveyed by the icon alone.</summary>
    public string KindText { get; }

    public string Glyph { get; }

    public string SizeText { get; }

    /// <summary>Share of the folder being shown, 0-100, for the proportional bar.</summary>
    public double Percent { get; }

    public string PercentText { get; }

    public string AutomationName { get; }

    public DiskNode? Folder { get; private init; }

    public DiskFile? File { get; private init; }

    /// <summary>The folder this row was listed in (needed to update totals after a Recycle Bin move).</summary>
    public DiskNode? Owner { get; private init; }

    public bool IsFolder => Folder is not null;

    public bool IsFile => File is not null;

    /// <summary>Folders can be opened to look inside.</summary>
    public bool CanOpen => IsFolder;

    /// <summary>Only ordinary files in the user's personal folders; see <see cref="DiskFile.CanRecycle"/>.</summary>
    public bool CanRecycle => File is { CanRecycle: true };

    public bool CanShowInFolder => IsFolder || IsFile;

    /// <summary>Full path for "Show in folder"; empty for the remainder row.</summary>
    public string Path => Folder?.Path ?? File?.FullPath ?? string.Empty;

    public static DiskRowViewModel ForFolder(DiskNode folder, DiskNode parent) =>
        new(folder.Name, "Folder", FolderGlyph, folder.TotalBytes, parent.TotalBytes) { Folder = folder, Owner = parent };

    public static DiskRowViewModel ForFile(DiskFile file, DiskNode owner) =>
        new(file.Name, "File", FileGlyph, file.Bytes, owner.TotalBytes) { File = file, Owner = owner };

    public static DiskRowViewModel ForOther(long bytes, DiskNode parent) =>
        new("Everything else", "Smaller items", OtherGlyph, bytes, parent.TotalBytes) { Owner = parent };
}
