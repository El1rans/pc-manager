namespace PCManager.Core.RemoteSupport;

/// <summary>
/// Parses <c>ad.anynet.id=</c> and <c>ad.anynet.alias=</c> out of AnyDesk's own config file text
/// (<c>system.conf</c>, or <c>service.conf</c> on older versions) - the fallback used when
/// <c>AnyDesk.exe --get-id</c>/<c>--get-alias</c> fails. A pure function over the file's text so it
/// is unit-testable without touching the real disk; see <see cref="AnyDeskConfigReader"/> for the
/// file access itself.
/// </summary>
public static class AnyDeskConfigParser
{
    private const string IdKey = "ad.anynet.id";
    private const string AliasKey = "ad.anynet.alias";

    /// <summary>Parses <paramref name="configText"/>, returning the ID and alias values found (each
    /// null if the key is absent or its value is blank). Tolerant of Windows (CRLF) and Unix (LF)
    /// line endings and of extra spaces around the <c>=</c>.</summary>
    public static (string? Id, string? Alias) Parse(string configText)
    {
        ArgumentNullException.ThrowIfNull(configText);

        string? id = null;
        string? alias = null;

        foreach (var rawLine in configText.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length == 0)
            {
                continue;
            }

            if (key.Equals(IdKey, StringComparison.Ordinal))
            {
                id = value;
            }
            else if (key.Equals(AliasKey, StringComparison.Ordinal))
            {
                alias = value;
            }
        }

        return (id, alias);
    }
}
