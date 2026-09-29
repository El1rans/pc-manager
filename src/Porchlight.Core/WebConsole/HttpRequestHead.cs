namespace Porchlight.Core.WebConsole;

/// <summary>The request line and headers of one HTTP request, as parsed by
/// <see cref="HttpRequestHeadParser"/>. The web console never reads a request body.</summary>
/// <param name="Method">Request method, e.g. <c>GET</c>.</param>
/// <param name="Path">Request path without its query string, e.g. <c>/api/stats</c>.</param>
/// <param name="Headers">Header values keyed by name, case-insensitively.</param>
public sealed record HttpRequestHead(string Method, string Path, IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>The bearer token from the <c>Authorization</c> header, or null when there is none.</summary>
    public string? BearerToken
    {
        get
        {
            const string scheme = "Bearer ";
            if (!Headers.TryGetValue("Authorization", out var value) ||
                !value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return value[scheme.Length..].Trim();
        }
    }
}
