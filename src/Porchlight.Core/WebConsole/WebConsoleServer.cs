using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.WebConsole;

/// <summary>
/// <see cref="IWebConsoleServer"/> on a plain <see cref="TcpListener"/>: no admin rights or URL
/// reservation needed (unlike <c>HttpListener</c>/http.sys), and no web framework shipped with the
/// app. It only has to speak enough HTTP/1.1 for a browser's simple GETs - one request per
/// connection, then close - with hard limits on request size, read time and concurrent connections
/// (see <see cref="WebConsoleOptions"/>). Routing, and with it the read-only guarantee, lives in
/// <see cref="WebConsoleRouter"/>.
/// </summary>
public sealed class WebConsoleServer : IWebConsoleServer, IDisposable
{
    private readonly WebConsoleRouter _router;
    private readonly ILogger<WebConsoleServer> _logger;
    private readonly object _lock = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _activeConnections;
    private bool _disposed;

    public WebConsoleServer(WebConsoleRouter router, ILogger<WebConsoleServer> logger)
    {
        _router = router;
        _logger = logger;
    }

    public WebConsoleState State { get; private set; }

    public int Port { get; private set; }

    public string? ErrorMessage { get; private set; }

    public event EventHandler? StateChanged;

    public void Start(int port, string accessKey)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            StopCore();
            StartCore(port, accessKey);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        bool wasRunning;
        lock (_lock)
        {
            wasRunning = State != WebConsoleState.Stopped;
            StopCore();
            State = WebConsoleState.Stopped;
            Port = 0;
            ErrorMessage = null;
        }

        if (wasRunning)
        {
            _logger.LogInformation("Web console stopped.");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    /// <summary>Listens on every interface - IPv6 and IPv4 together where IPv6 is available.</summary>
    private static TcpListener CreateListener(int port)
    {
        TcpListener listener;
        if (Socket.OSSupportsIPv6)
        {
            listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.DualMode = true;
        }
        else
        {
            listener = new TcpListener(IPAddress.Any, port);
        }

        // Stops another program binding the same port more specifically and intercepting requests.
        listener.ExclusiveAddressUse = true;
        return listener;
    }

    private static string DescribeStartFailure(SocketError error, int port) => error switch
    {
        SocketError.AddressAlreadyInUse =>
            $"Port {port} is already used by another program. Pick a different port and turn the web console on again.",
        SocketError.AccessDenied =>
            $"Windows did not allow Porchlight to use port {port}. Pick a different port and turn the web console on again.",
        _ => $"The web console could not start on port {port}. Pick a different port, or restart your PC and try again.",
    };

    /// <summary>Caller holds <see cref="_lock"/>.</summary>
    private void StopCore()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _listener?.Stop();
        _listener?.Dispose();
        _listener = null;
    }

    /// <summary>Caller holds <see cref="_lock"/>.</summary>
    private void StartCore(int port, string accessKey)
    {
        var listener = CreateListener(port);
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            listener.Dispose();
            _logger.LogWarning(ex, "Web console could not listen on port {Port}.", port);
            State = WebConsoleState.Failed;
            Port = 0;
            ErrorMessage = DescribeStartFailure(ex.SocketErrorCode, port);
            return;
        }

        var cts = new CancellationTokenSource();
        _listener = listener;
        _cts = cts;
        State = WebConsoleState.Running;
        var boundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        Port = boundPort;
        ErrorMessage = null;
        _logger.LogInformation("Web console listening on port {Port}.", boundPort);

        _ = Task.Run(() => AcceptLoopAsync(listener, accessKey, cts.Token), CancellationToken.None);
    }

    private async Task AcceptLoopAsync(TcpListener listener, string accessKey, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException ||
                                       cancellationToken.IsCancellationRequested)
            {
                // Expected: Stop() cancelled the token and closed the listener.
                return;
            }
            catch (SocketException ex)
            {
                // One failed accept (e.g. the client reset the connection mid-handshake) must not
                // stop the console; keep accepting.
                _logger.LogDebug(ex, "Web console accept failed; continuing.");
                continue;
            }

            if (Interlocked.Increment(ref _activeConnections) > WebConsoleOptions.MaxConcurrentConnections)
            {
                Interlocked.Decrement(ref _activeConnections);
                _logger.LogDebug("Web console dropped a connection: too many open at once.");
                client.Dispose();
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, accessKey, cancellationToken), CancellationToken.None);
        }
    }

    /// <summary>Serves exactly one request, then closes the connection. Never throws: every failure
    /// is a problem with that one connection only.</summary>
    private async Task HandleClientAsync(TcpClient client, string accessKey, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();

                (bool TooLarge, string? Head) read;
                using (var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    readTimeout.CancelAfter(WebConsoleOptions.RequestReadTimeout);
                    read = await HttpRequestHeadReader.ReadAsync(stream, readTimeout.Token).ConfigureAwait(false);
                }

                WebConsoleResponse response;
                var isHeadRequest = false;
                if (read.TooLarge)
                {
                    response = WebConsoleResponse.Text(431, "Request Header Fields Too Large", "Request too large.");
                }
                else if (read.Head is null)
                {
                    // The client went away without sending a whole request; nobody to answer.
                    return;
                }
                else if (!HttpRequestHeadParser.TryParse(read.Head, out var request))
                {
                    response = WebConsoleResponse.Text(400, "Bad Request", "Bad request.");
                }
                else
                {
                    isHeadRequest = string.Equals(request.Method, "HEAD", StringComparison.Ordinal);
                    response = await _router.RouteAsync(request, accessKey, cancellationToken).ConfigureAwait(false);
                }

                using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                writeTimeout.CancelAfter(WebConsoleOptions.RequestReadTimeout);
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response.FormatHead()), writeTimeout.Token).ConfigureAwait(false);
                if (!isHeadRequest)
                {
                    await stream.WriteAsync(response.Body, writeTimeout.Token).ConfigureAwait(false);
                }

                await stream.FlushAsync(writeTimeout.Token).ConfigureAwait(false);
                client.Client.Shutdown(SocketShutdown.Send);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            // Timed out, the client disconnected, or the console is stopping - all routine.
            _logger.LogDebug(ex, "Web console connection ended early.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Web console failed to answer a request.");
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
        }
    }
}
