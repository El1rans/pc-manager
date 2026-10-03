using System.Xml;
using System.Xml.Linq;

namespace Porchlight.Core.Backup;

/// <summary>Reads a File History <c>Config*.xml</c> tolerantly: the schema is undocumented, so it
/// only looks for a target element and an optional enabled flag.</summary>
public static class FileHistoryConfigParser
{
    private static readonly string[] TargetElements = ["TargetUrl", "TargetName"];
    private static readonly string[] EnabledElements = ["UserEnabled", "Enabled"];

    /// <summary>Parses one config document. Returns null if it is not readable XML. Enabled defaults
    /// to true when a target exists but no enabled flag does.</summary>
    public static (bool IsConfigured, bool IsEnabled)? Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }

        var configured = TargetElements.Any(name => ElementText(document, name) is { Length: > 0 });
        if (!configured)
        {
            return (false, false);
        }

        var flag = EnabledElements.Select(name => ElementText(document, name)).FirstOrDefault(text => text is not null);
        var enabled = flag is null || !flag.Equals("false", StringComparison.OrdinalIgnoreCase);
        return (true, enabled);
    }

    private static string? ElementText(XDocument document, string localName) =>
        document.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim();
}
