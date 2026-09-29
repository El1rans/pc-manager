using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.SelfUpdate;

/// <inheritdoc cref="IReleaseChecker"/>
/// <remarks>Sends one plain, unauthenticated GET to GitHub's releases API with a
/// <c>User-Agent: Porchlight/&lt;version&gt;</c> header and nothing else identifying - see
/// <c>docs/CODE_SIGNING_POLICY.md</c>'s privacy statement.</remarks>
public sealed partial class GitHubReleaseChecker : IReleaseChecker, IDisposable
{
    private const long MaxResponseBytes = 1_000_000;

    private static readonly Uri LatestReleaseUrl = new($"https://api.github.com/repos/{ReleaseParser.Repository}/releases/latest");
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly IRunningAppInfo _appInfo;
    private readonly ILogger<GitHubReleaseChecker> _logger;

    public GitHubReleaseChecker(HttpMessageHandler handler, IRunningAppInfo appInfo, ILogger<GitHubReleaseChecker> logger)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = RequestTimeout };
        _appInfo = appInfo;
        _logger = logger;
    }

    public async Task<SelfUpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Porchlight", _appInfo.Version.ToString(3)));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LogNoRelease();
                return SelfUpdateCheckResult.UpToDate;
            }

            if (!response.IsSuccessStatusCode)
            {
                // 403/429 is GitHub's unauthenticated rate limit; either way this is just "couldn't check".
                LogHttpStatus((int)response.StatusCode);
                return SelfUpdateCheckResult.CouldNotCheck;
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                LogOversizedReply();
                return SelfUpdateCheckResult.CouldNotCheck;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var release = ReleaseParser.Parse(json);
            if (release is null)
            {
                LogUnusableReply();
                return SelfUpdateCheckResult.CouldNotCheck;
            }

            return ReleaseParser.IsNewer(release.Version, _appInfo.Version)
                ? new SelfUpdateCheckResult(SelfUpdateCheckStatus.UpdateAvailable, release)
                : SelfUpdateCheckResult.UpToDate;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // Offline, DNS failure or the request timed out: expected on a PC without internet.
            LogNetworkFailure(ex.Message);
            return SelfUpdateCheckResult.CouldNotCheck;
        }
    }

    public void Dispose() => _httpClient.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "No Porchlight release has been published yet.")]
    private partial void LogNoRelease();

    [LoggerMessage(Level = LogLevel.Information, Message = "Couldn't check for a new Porchlight version: GitHub replied {StatusCode}.")]
    private partial void LogHttpStatus(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring an unexpectedly large release reply from GitHub.")]
    private partial void LogOversizedReply();

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub's latest release reply wasn't a usable Porchlight release; ignoring it.")]
    private partial void LogUnusableReply();

    [LoggerMessage(Level = LogLevel.Information, Message = "Couldn't check for a new Porchlight version ({Reason}).")]
    private partial void LogNetworkFailure(string reason);
}
