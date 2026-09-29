using System.Diagnostics.CodeAnalysis;

namespace Porchlight.Core.WebConsole;

/// <summary>
/// Parses the head (request line plus headers) of an HTTP/1.x request. Deliberately strict: the
/// web console only ever talks to browsers sending simple GETs, so anything unusual - an absolute
/// or over-long target, a malformed or duplicated header, an unknown protocol version - is simply
/// rejected rather than interpreted.
/// </summary>
public static class HttpRequestHeadParser
{
    /// <summary>Characters that end the path part of a request target.</summary>
    private static readonly char[] PathTerminators = ['?', '#'];

    /// <summary>Punctuation allowed in an RFC 9110 "token" besides letters and digits.</summary>
    private const string TokenPunctuation = "!#$%&'*+-.^_`|~";

    /// <summary>Parses <paramref name="head"/> (everything before the blank line that ends the
    /// headers, without that blank line). Returns false when it is not a well-formed request.</summary>
    public static bool TryParse(string head, [NotNullWhen(true)] out HttpRequestHead? request)
    {
        request = null;

        var lines = head.Split("\r\n");
        if (lines.Length == 0 || !TryParseRequestLine(lines[0], out var method, out var path))
        {
            return false;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return false;
            }

            var name = line[..colon];
            if (!IsToken(name))
            {
                return false;
            }

            // A repeated header (e.g. two Authorization lines) is ambiguous - reject it rather than
            // guess which one counts.
            if (!headers.TryAdd(name, line[(colon + 1)..].Trim()))
            {
                return false;
            }
        }

        request = new HttpRequestHead(method, path, headers);
        return true;
    }

    private static bool TryParseRequestLine(string line, out string method, out string path)
    {
        method = string.Empty;
        path = string.Empty;

        var parts = line.Split(' ');
        if (parts.Length != 3)
        {
            return false;
        }

        var (candidateMethod, target, version) = (parts[0], parts[1], parts[2]);
        if (!IsToken(candidateMethod) ||
            !version.StartsWith("HTTP/1.", StringComparison.Ordinal) ||
            target.Length == 0 ||
            target.Length > WebConsoleOptions.MaxRequestTargetLength ||
            target[0] != '/')
        {
            return false;
        }

        foreach (var c in target)
        {
            if (c <= ' ' || c >= 0x7F)
            {
                return false;
            }
        }

        var queryStart = target.IndexOfAny(PathTerminators);
        method = candidateMethod;
        path = queryStart < 0 ? target : target[..queryStart];
        return true;
    }

    /// <summary>An RFC 9110 "token": the characters allowed in a method or header name.</summary>
    private static bool IsToken(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var c in value)
        {
            var isTokenChar = char.IsAsciiLetterOrDigit(c) || TokenPunctuation.Contains(c, StringComparison.Ordinal);
            if (!isTokenChar)
            {
                return false;
            }
        }

        return true;
    }
}
