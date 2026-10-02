using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Porchlight.Core.Startup;

/// <summary>
/// Turns a <c>StartupInfo</c> XML file into <see cref="StartupInfoRecord"/>s. Pure and tolerant:
/// Windows doesn't document the schema, so any element that carries an image/path field plus a
/// CPU and/or disk field (as an attribute or a child element) counts, and unit hints in field
/// names or values (<c>Ms</c>, <c>Sec</c>, <c>KB</c>, <c>MB</c>) are honoured. Garbage gives an empty list.
/// </summary>
public static class StartupInfoParser
{
    private const double MillisecondsPerSecond = 1000;
    private const long BytesPerKilobyte = 1024;
    private const long BytesPerMegabyte = 1024 * 1024;

    public static IReadOnlyList<StartupInfoRecord> Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        try
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, Settings());
            return Extract(XDocument.Load(reader));
        }
        catch (XmlException)
        {
            return [];
        }
    }

    public static IReadOnlyList<StartupInfoRecord> Parse(Stream stream)
    {
        try
        {
            using var reader = XmlReader.Create(stream, Settings());
            return Extract(XDocument.Load(reader));
        }
        catch (XmlException)
        {
            return [];
        }
    }

    private static XmlReaderSettings Settings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    private static List<StartupInfoRecord> Extract(XDocument document)
    {
        var records = new List<StartupInfoRecord>();
        foreach (var element in document.Descendants())
        {
            string? image = null;
            double? cpu = null;
            long? disk = null;

            foreach (var (name, value) in Fields(element))
            {
                var lower = name.ToLowerInvariant();
                if (image is null && IsImageField(lower) && LooksLikePath(value))
                {
                    image = lower.Contains("commandline", StringComparison.Ordinal)
                        ? StartupCommandParser.ExtractExecutablePath(value)
                        : value.Trim();
                }
                else if (cpu is null && lower.Contains("cpu", StringComparison.Ordinal) && TryParseNumber(value, out var cpuNumber, out var cpuSuffix))
                {
                    cpu = ToMilliseconds(lower, cpuSuffix, cpuNumber);
                }
                else if (disk is null && IsDiskField(lower) && TryParseNumber(value, out var diskNumber, out var diskSuffix))
                {
                    disk = ToBytes(lower, diskSuffix, diskNumber);
                }
            }

            if (!string.IsNullOrWhiteSpace(image) && (cpu is not null || disk is not null))
            {
                records.Add(new StartupInfoRecord(image, cpu ?? 0, disk ?? 0));
            }
        }

        return records;
    }

    private static IEnumerable<(string Name, string Value)> Fields(XElement element)
    {
        foreach (var attribute in element.Attributes())
        {
            yield return (attribute.Name.LocalName, attribute.Value);
        }

        foreach (var child in element.Elements().Where(c => !c.HasElements))
        {
            yield return (child.Name.LocalName, child.Value);
        }
    }

    // "name" covers the shape Task Manager's own files are believed to use -
    // <Process Name="C:\...pp.exe"><CpuUsage>ms</CpuUsage><DiskUsage>bytes</DiskUsage>... - where the
    // full image path sits in the Name attribute; LooksLikePath keeps plain names out. "ParentName"
    // is excluded so a child's record is never attributed to its parent (explorer.exe).
    private static bool IsImageField(string lowerName) =>
        lowerName == "name" ||
        lowerName.Contains("image", StringComparison.Ordinal) ||
        lowerName.Contains("path", StringComparison.Ordinal) ||
        lowerName.Contains("commandline", StringComparison.Ordinal) ||
        lowerName.Contains("exe", StringComparison.Ordinal);

    private static bool IsDiskField(string lowerName) =>
        lowerName.Contains("disk", StringComparison.Ordinal) ||
        (lowerName.Contains("io", StringComparison.Ordinal) && lowerName.Contains("byte", StringComparison.Ordinal));

    private static bool LooksLikePath(string value) =>
        value.Contains('\\', StringComparison.Ordinal) || value.Contains(".exe", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseNumber(string text, out double number, out string suffix)
    {
        number = 0;
        suffix = string.Empty;
        var trimmed = text.Trim();
        var end = 0;
        while (end < trimmed.Length && (char.IsAsciiDigit(trimmed[end]) || trimmed[end] is '.' or '-'))
        {
            end++;
        }

        if (end == 0 ||
            !double.TryParse(trimmed.AsSpan(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
            double.IsNaN(number) || double.IsInfinity(number) || number < 0)
        {
            return false;
        }

        suffix = trimmed[end..].Trim().ToLowerInvariant();
        return true;
    }

    private static double ToMilliseconds(string fieldName, string suffix, double number)
    {
        // A unit written after the number wins; otherwise the field name's ending decides.
        var unit = suffix.Length > 0 ? suffix : fieldName;
        var isSeconds = !unit.EndsWith("ms", StringComparison.Ordinal) &&
            (unit == "s" || unit.EndsWith("sec", StringComparison.Ordinal) || unit.EndsWith("secs", StringComparison.Ordinal) ||
             unit.EndsWith("seconds", StringComparison.Ordinal));
        return isSeconds ? number * MillisecondsPerSecond : number;
    }

    private static long ToBytes(string fieldName, string suffix, double number)
    {
        var unit = suffix.Length > 0 ? suffix : fieldName;
        var factor = unit.EndsWith("mb", StringComparison.Ordinal) ? BytesPerMegabyte
            : unit.EndsWith("kb", StringComparison.Ordinal) ? BytesPerKilobyte
            : 1;
        return (long)(number * factor);
    }
}
