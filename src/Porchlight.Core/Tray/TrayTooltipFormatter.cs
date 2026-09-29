using System.Globalization;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Tray;

/// <summary>Builds the tray icon's tooltip text. Pure; the result always fits Windows'
/// 127-character tooltip limit.</summary>
public static class TrayTooltipFormatter
{
    /// <summary>Windows' NOTIFYICONDATA tooltip holds 128 UTF-16 units including the terminator.</summary>
    public const int MaxLength = 127;

    private const string Title = "Porchlight";

    public static string Format(QuickStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var parts = new List<string>();
        if (stats.CpuPercent is { } cpu)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"CPU {Math.Round(cpu):0}%"));
        }

        if (stats.MemoryPercent is { } memory)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"memory {Math.Round(memory):0}%"));
        }

        if (stats.SystemDriveFreeBytes is { } free)
        {
            var drive = (stats.SystemDriveName ?? "C:").TrimEnd('\\', '/');
            parts.Add($"{drive} {ByteFormatter.FormatBytes(free)} free");
        }

        var text = parts.Count == 0 ? Title : $"{Title} - {string.Join(", ", parts)}";
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
