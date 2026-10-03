using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Porchlight.Core.WebConsole;

/// <summary>
/// Maps a parsed request to a response. The whole surface of the web console is here, and it is
/// read-only by construction: only <c>GET</c>/<c>HEAD</c> are accepted, the only routes are the
/// page's three static files and the four <c>/api/*</c> read-only views (stats, updates, startup,
/// security), and nothing reachable from any route changes anything on the PC. Every <c>/api/*</c>
/// route requires the access key.
/// </summary>
public sealed class WebConsoleRouter
{
    public const string StatsPath = "/api/stats";
    public const string UpdatesPath = "/api/updates";
    public const string StartupPath = "/api/startup";
    public const string SecurityPath = "/api/security";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly IReadOnlyList<KeyValuePair<string, string>> AllowHeader = [new("Allow", "GET, HEAD")];

    private static readonly IReadOnlyList<KeyValuePair<string, string>> UnauthorizedHeaders =
        [new("WWW-Authenticate", "Bearer realm=\"Porchlight\"")];

    /// <summary>Static page files by request path: (embedded resource name, content type).</summary>
    private static readonly Dictionary<string, (string Resource, string ContentType)> StaticFiles =
        new(StringComparer.Ordinal)
        {
            ["/"] = ("index.html", "text/html; charset=utf-8"),
            ["/index.html"] = ("index.html", "text/html; charset=utf-8"),
            ["/app.js"] = ("app.js", "text/javascript; charset=utf-8"),
            ["/app.css"] = ("app.css", "text/css; charset=utf-8"),
        };

    private readonly IWebConsoleStatsSource _statsSource;
    private readonly IWebConsoleDetailsSource _detailsSource;
    private readonly Dictionary<string, byte[]> _staticFileCache = new(StringComparer.Ordinal);
    private readonly object _staticFileCacheLock = new();

    public WebConsoleRouter(IWebConsoleStatsSource statsSource, IWebConsoleDetailsSource detailsSource)
    {
        _statsSource = statsSource;
        _detailsSource = detailsSource;
    }

    /// <param name="request">The parsed request.</param>
    /// <param name="accessKey">The key <c>/api/stats</c> requires, as a bearer token.</param>
    public async Task<WebConsoleResponse> RouteAsync(HttpRequestHead request, string accessKey, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Method, "GET", StringComparison.Ordinal) &&
            !string.Equals(request.Method, "HEAD", StringComparison.Ordinal))
        {
            // Read-only: there is nothing to POST, PUT or DELETE to, on any path.
            return WebConsoleResponse.Text(405, "Method Not Allowed", "This console is read-only.") with
            {
                ExtraHeaders = AllowHeader,
            };
        }

        if (StaticFiles.TryGetValue(request.Path, out var file))
        {
            return new WebConsoleResponse(200, "OK", file.ContentType, ReadStaticFile(file.Resource), []);
        }

        if (IsApiPath(request.Path))
        {
            if (!AccessKeyGenerator.Matches(accessKey, request.BearerToken))
            {
                return WebConsoleResponse.Text(401, "Unauthorized", "A valid access key is required.") with
                {
                    ExtraHeaders = UnauthorizedHeaders,
                };
            }

            object payload = request.Path switch
            {
                StatsPath => await _statsSource.GetAsync(cancellationToken).ConfigureAwait(false),
                UpdatesPath => await _detailsSource.GetUpdatesAsync(cancellationToken).ConfigureAwait(false),
                StartupPath => await _detailsSource.GetStartupAsync(cancellationToken).ConfigureAwait(false),
                _ => await _detailsSource.GetSecurityAsync(cancellationToken).ConfigureAwait(false),
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), JsonOptions);
            return new WebConsoleResponse(200, "OK", "application/json; charset=utf-8", json, []);
        }

        return WebConsoleResponse.Text(404, "Not Found", "Not found.");
    }

    private static bool IsApiPath(string path) =>
        path is StatsPath or UpdatesPath or StartupPath or SecurityPath;

    private byte[] ReadStaticFile(string name)
    {
        lock (_staticFileCacheLock)
        {
            if (_staticFileCache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var resourceName = $"Porchlight.WebConsole.{name}";
            using var stream = typeof(WebConsoleRouter).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded web console file '{resourceName}'.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            _staticFileCache[name] = bytes;
            return bytes;
        }
    }
}
