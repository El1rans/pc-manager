using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkProbe"/>
public sealed class NetworkProbe : INetworkProbe, IDisposable
{
    /// <summary>Windows' own connectivity test address (plain HTTP on purpose, so a sign-in page
    /// that intercepts it can be told apart from "no internet").</summary>
    public const string ConnectTestUrl = "http://www.msftconnecttest.com/connecttest.txt";

    /// <summary>The exact body that address returns when the internet is reachable.</summary>
    public const string ConnectTestExpectedBody = "Microsoft Connect Test";

    private const string DnsTestHost = "www.microsoft.com";
    private const int PingAttempts = 2;
    private const int PingTimeoutMilliseconds = 2000;
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan InternetTimeout = TimeSpan.FromSeconds(8);

    private readonly INetworkStatusProvider _statusProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NetworkProbe> _logger;

    /// <param name="statusProvider">Source of the adapter and router address.</param>
    /// <param name="handler">HTTP transport; must not follow redirects (so a sign-in page is seen).</param>
    /// <param name="logger">Logger.</param>
    public NetworkProbe(INetworkStatusProvider statusProvider, HttpMessageHandler handler, ILogger<NetworkProbe> logger)
    {
        _statusProvider = statusProvider;
        _httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = InternetTimeout };
        _logger = logger;
    }

    public bool IsAdapterUp() => _statusProvider.GetStatus().AdapterName is not null;

    public async Task<bool> PingGatewayAsync(CancellationToken cancellationToken)
    {
        var gateway = await Task.Run(() => _statusProvider.GetStatus().Gateway, cancellationToken).ConfigureAwait(false);
        if (gateway is null || !IPAddress.TryParse(gateway, out var address))
        {
            return false;
        }

        using var ping = new Ping();
        for (var attempt = 0; attempt < PingAttempts; attempt++)
        {
            try
            {
                var reply = await ping.SendPingAsync(address, PingTimeoutMilliseconds).WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }
            }
            catch (PingException ex)
            {
                // A failed ping is the answer we are looking for; log and try once more.
                _logger.LogDebug(ex, "Ping to the router failed.");
            }
        }

        return false;
    }

    public async Task<bool> ResolveDnsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DnsTimeout);
            var addresses = await Dns.GetHostAddressesAsync(DnsTestHost, timeout.Token).ConfigureAwait(false);
            return addresses.Length > 0;
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or OperationCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            // Failure or timeout is the answer ("name lookup does not work").
            if (_logger.IsEnabled(LogLevel.Debug)) { _logger.LogDebug(ex, "Name lookup for {Host} failed.", DnsTestHost); }
            return false;
        }
    }

    public async Task<InternetCheckResult> CheckInternetAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ConnectTestUrl, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                return InternetCheckResult.CaptivePortal;
            }

            if (!response.IsSuccessStatusCode)
            {
                return InternetCheckResult.Unreachable;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return body.Trim() == ConnectTestExpectedBody
                ? InternetCheckResult.Reachable
                : InternetCheckResult.CaptivePortal;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            // No answer (or a timeout) is the result we report.
            _logger.LogDebug(ex, "Internet check failed.");
            return InternetCheckResult.Unreachable;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
