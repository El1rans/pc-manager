using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="ISpeedTestService"/>
public sealed class SpeedTestService : ISpeedTestService, IDisposable
{
    private const string UploadUrl = "https://speed.cloudflare.com/__up";
    private const int LatencyWarmUpRequests = 1;
    private const int LatencySamples = 5;
    private const int ReadBufferSize = 81_920;

    private static readonly TimeSpan DownloadBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UploadBudget = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly long[] DownloadSizes = [1_000_000, 5_000_000, 25_000_000, 100_000_000];
    private static readonly int[] UploadSizes = [250_000, 1_000_000, 4_000_000];

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SpeedTestService> _logger;

    public SpeedTestService(HttpMessageHandler handler, TimeProvider timeProvider, ILogger<SpeedTestService> logger)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = RequestTimeout };
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SpeedTestResult?> RunAsync(IProgress<SpeedTestPhase>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(SpeedTestPhase.Latency);
        var latency = await MeasureLatencyAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(SpeedTestPhase.Download);
        var download = await MeasureDownloadAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(SpeedTestPhase.Upload);
        var upload = await MeasureUploadAsync(cancellationToken).ConfigureAwait(false);

        if (latency is null && download is null && upload is null)
        {
            return null;
        }

        return new SpeedTestResult(latency, download, upload, _timeProvider.GetUtcNow());
    }

    private async Task<double?> MeasureLatencyAsync(CancellationToken cancellationToken)
    {
        var samples = new List<double>();
        var url = DownloadUrl(0);
        try
        {
            for (var i = 0; i < LatencyWarmUpRequests + LatencySamples; i++)
            {
                var start = _timeProvider.GetTimestamp();
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var elapsed = _timeProvider.GetElapsedTime(start);

                // The first request also pays for connection and security setup, so it is not counted.
                if (i >= LatencyWarmUpRequests)
                {
                    samples.Add(elapsed.TotalMilliseconds);
                }
            }
        }
        catch (Exception ex) when (IsNetworkFailure(ex, cancellationToken))
        {
            _logger.LogDebug(ex, "Latency measurement failed.");
            return null;
        }

        return samples.Count == 0 ? null : SpeedMath.Median(samples);
    }

    private async Task<double?> MeasureDownloadAsync(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        var start = _timeProvider.GetTimestamp();
        try
        {
            foreach (var size in DownloadSizes)
            {
                var remaining = DownloadBudget - _timeProvider.GetElapsedTime(start);
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                using var budget = new CancellationTokenSource(remaining, _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
                try
                {
                    var url = DownloadUrl(size);
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
                    var buffer = new byte[ReadBufferSize];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, linked.Token).ConfigureAwait(false)) > 0)
                    {
                        totalBytes += read;
                        if (_timeProvider.GetElapsedTime(start) >= DownloadBudget)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The time budget ran out mid-transfer; what arrived so far still counts.
                    break;
                }
            }
        }
        catch (Exception ex) when (IsNetworkFailure(ex, cancellationToken))
        {
            _logger.LogDebug(ex, "Download measurement failed.");
        }

        return totalBytes > 0 ? SpeedMath.ToMbps(totalBytes, _timeProvider.GetElapsedTime(start)) : null;
    }

    private async Task<double?> MeasureUploadAsync(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        var totalTime = TimeSpan.Zero;
        var overallStart = _timeProvider.GetTimestamp();
        var payload = new byte[UploadSizes[^1]];
        try
        {
            foreach (var size in UploadSizes)
            {
                var remaining = UploadBudget - _timeProvider.GetElapsedTime(overallStart);
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                using var budget = new CancellationTokenSource(remaining, _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
                try
                {
                    using var content = new ByteArrayContent(payload, 0, size);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    var start = _timeProvider.GetTimestamp();
                    using var response = await _httpClient.PostAsync(UploadUrl, content, linked.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    totalTime += _timeProvider.GetElapsedTime(start);
                    totalBytes += size;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Budget ran out during this upload; keep what finished earlier.
                    break;
                }
            }
        }
        catch (Exception ex) when (IsNetworkFailure(ex, cancellationToken))
        {
            _logger.LogDebug(ex, "Upload measurement failed.");
        }

        return totalBytes > 0 ? SpeedMath.ToMbps(totalBytes, totalTime) : null;
    }

    private static string DownloadUrl(long bytes) => $"https://speed.cloudflare.com/__down?bytes={bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static bool IsNetworkFailure(Exception ex, CancellationToken userToken) =>
        !userToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException;

    public void Dispose() => _httpClient.Dispose();
}
