using System.Text;

namespace Porchlight.Core.Health;

/// <summary>
/// Cleans raw SFC/DISM output. <c>sfc.exe</c> writes UTF-16; <c>IProcessRunner</c> decodes stdout as
/// UTF-8, so every character arrives followed by a NUL. Stripping NULs restores the ASCII text
/// (localised, non-ASCII text cannot be recovered this way - see docs/specs/14-system-health.md).
/// </summary>
public static class RepairOutputCleaner
{
    /// <summary>Removes NUL characters (and the BOM if one leaked through) and trims the result.</summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (c is not ('\0' or '﻿' or '�'))
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Cleans every line, drops the empty ones and joins the rest with single spaces, so a
    /// sentence the tool wrapped across lines can still be matched.</summary>
    public static string JoinNormalized(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var parts = lines.Select(Clean).Where(l => l.Length > 0);
        return string.Join(' ', parts);
    }
}
