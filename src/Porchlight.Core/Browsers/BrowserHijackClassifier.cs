using System.Globalization;

namespace Porchlight.Core.Browsers;

/// <summary>The verdict and the plain text to show for one setting.</summary>
public sealed record HijackClassification(HijackStatus Status, string Value);

/// <summary>
/// Pure rules deciding whether a home page, startup page, new-tab page or search address is a
/// well-known one (<see cref="HijackStatus.Ok"/>), something else (<see cref="HijackStatus.Changed"/>),
/// or something else that a policy on this PC forces (<see cref="HijackStatus.ForcedByPolicy"/>).
/// Advice only: an unfamiliar address is not proof of anything. See docs/specs/32-browser-hijack-check.md.
/// </summary>
public static class BrowserHijackClassifier
{
    public const string DefaultValue = "Browser default";
    public const string OwnPageValue = "The browser's own page";
    public const string FileValue = "A file on this PC";
    public const string UnusualValue = "An unusual address";

    private const int MaxHostsShown = 3;
    private const int MaxRawLength = 60;
    private const string GoogleBaseUrlToken = "{google:baseurl}";
    private const string GoogleHost = "google.com";

    /// <summary>Base names of well-known search providers and home pages; matched against the
    /// registrable part of the host (google.com, google.co.uk, ...), never a sub-domain trick.</summary>
    private static readonly HashSet<string> KnownNames = new(StringComparer.Ordinal)
    {
        "google", "bing", "duckduckgo", "yahoo", "ecosia", "brave", "startpage", "msn", "microsoft", "mozilla",
    };

    /// <summary>Second-level labels used under country domains (bing.co.uk style).</summary>
    private static readonly HashSet<string> CountrySecondLevels = new(StringComparer.Ordinal)
    {
        "co", "com", "org", "net", "ac", "gov",
    };

    private static readonly HashSet<string> BrowserSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "edge", "brave", "about", "chrome-search", "chrome-untrusted", "chrome-native", "moz-extension",
    };

    /// <summary>Classifies one address. Empty means "not set", which is the browser default.</summary>
    public static HijackClassification Classify(string? address, bool forcedByPolicy)
    {
        var raw = address?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return new(HijackStatus.Ok, DefaultValue);
        }

        if (raw.StartsWith(GoogleBaseUrlToken, StringComparison.OrdinalIgnoreCase))
        {
            return new(HijackStatus.Ok, GoogleHost);
        }

        // Search templates hold placeholders such as {searchTerms}; swap them so the address parses.
        var parseable = raw.Replace("{searchTerms}", "x", StringComparison.OrdinalIgnoreCase);
        Uri? uri = null;
        if (Uri.TryCreate(parseable, UriKind.Absolute, out var parsed) && !parsed.Scheme.Contains('.', StringComparison.Ordinal))
        {
            uri = parsed;
        }
        else if (Uri.TryCreate("https://" + parseable, UriKind.Absolute, out var guessed)
            && guessed.Host.Contains('.', StringComparison.Ordinal))
        {
            // A bare "example.com/path" (or "example.com:8080", which parses as a scheme).
            uri = guessed;
        }

        if (uri is null)
        {
            return Unfamiliar(Truncate(raw), forcedByPolicy);
        }

        if (BrowserSchemes.Contains(uri.Scheme))
        {
            return new(HijackStatus.Ok, OwnPageValue);
        }

        if (uri.Scheme == Uri.UriSchemeFile)
        {
            return Unfamiliar(FileValue, forcedByPolicy);
        }

        if (uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))
        {
            return Unfamiliar(UnusualValue, forcedByPolicy);
        }

        var host = uri.Host.ToLower(CultureInfo.InvariantCulture);
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return IsKnownHost(host) ? new(HijackStatus.Ok, host) : Unfamiliar(host, forcedByPolicy);
    }

    /// <summary>Classifies several addresses (startup pages): the worst verdict wins and the distinct
    /// hosts are listed.</summary>
    public static HijackClassification ClassifyAll(IEnumerable<string> addresses, bool forcedByPolicy)
    {
        var results = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => Classify(a, forcedByPolicy))
            .ToList();
        if (results.Count == 0)
        {
            return new(HijackStatus.Ok, DefaultValue);
        }

        var worst = results.Max(r => r.Status);
        var values = results.Select(r => r.Value).Distinct(StringComparer.Ordinal).ToList();
        var text = string.Join(", ", values.Take(MaxHostsShown));
        if (values.Count > MaxHostsShown)
        {
            text += $" and {values.Count - MaxHostsShown} more";
        }

        return new(worst, text);
    }

    /// <summary>True for a well-known provider host (already lower-case, without "www.").</summary>
    public static bool IsKnownHost(string host)
    {
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2)
        {
            return false;
        }

        if (KnownNames.Contains(labels[^2]))
        {
            return true;
        }

        return labels.Length >= 3 && CountrySecondLevels.Contains(labels[^2]) && KnownNames.Contains(labels[^3]);
    }

    private static HijackClassification Unfamiliar(string value, bool forcedByPolicy) =>
        new(forcedByPolicy ? HijackStatus.ForcedByPolicy : HijackStatus.Changed, value);

    private static string Truncate(string raw) =>
        raw.Length <= MaxRawLength ? raw : raw[..MaxRawLength] + "...";
}
