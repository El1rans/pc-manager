namespace Porchlight.Core.RemoteSupport;

/// <summary>
/// Formats an AnyDesk address for display and for copying to the clipboard. A numeric ID is
/// grouped in threes from the right, like a thousands separator (<c>"123456789"</c> -&gt;
/// <c>"123 456 789"</c>, <c>"1234567890"</c> -&gt; <c>"1 234 567 890"</c>); an alias (anything
/// containing a letter or <c>@</c>) is left unchanged, since AnyDesk aliases are not digit groups.
/// </summary>
public static class AnyDeskIdFormatter
{
    /// <summary>Formats <paramref name="address"/> for display: grouped digits for a numeric ID,
    /// unchanged for an alias. Returns an empty string for null/blank input.</summary>
    public static string Format(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        var trimmed = address.Trim();
        return IsNumericId(trimmed) ? GroupDigits(trimmed) : trimmed;
    }

    /// <summary>The value to actually put on the clipboard for <paramref name="address"/>: the bare
    /// digits for a numeric ID (no spaces - what the helper needs to type into AnyDesk), or the
    /// alias with surrounding whitespace removed.</summary>
    public static string CopyValue(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        var trimmed = address.Trim();
        var digitsOnly = DigitsOnly(trimmed);

        // Accept both a bare ID ("123456789") and an already-grouped one ("123 456 789") - the
        // digits are the same either way once whitespace is stripped (any whitespace, not just a
        // plain space - e.g. a stray tab), so if the only non-digit characters were whitespace
        // (grouping), this is still a numeric ID.
        var withoutWhitespace = new string([.. trimmed.Where(c => !char.IsWhiteSpace(c))]);
        var isGroupedNumericId = digitsOnly.Length > 0 && withoutWhitespace == digitsOnly;

        return isGroupedNumericId ? digitsOnly : trimmed;
    }

    private static bool IsNumericId(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static string DigitsOnly(string value) =>
        new([.. value.Where(char.IsAsciiDigit)]);

    private static string GroupDigits(string digits)
    {
        var groups = new List<string>();
        for (var end = digits.Length; end > 0; end -= 3)
        {
            var start = Math.Max(0, end - 3);
            groups.Insert(0, digits[start..end]);
        }

        return string.Join(' ', groups);
    }
}
