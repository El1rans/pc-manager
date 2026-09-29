using System.Globalization;

namespace Porchlight.Core.Cleanup;

/// <summary>Parses the uninstall registry's <c>InstallDate</c> value, which installers write as
/// <c>yyyyMMdd</c> (or leave empty, zero, or as some other junk).</summary>
public static class InstallDateParser
{
    public static DateOnly? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
