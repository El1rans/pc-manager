using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.SelfUpdate;

/// <summary>Which step of preparing an update is running.</summary>
public enum UpdateDownloadPhase
{
    Downloading,
    Verifying,
}

/// <param name="Phase">The current step.</param>
/// <param name="Fraction">0..1 while <see cref="UpdateDownloadPhase.Downloading"/> and the size is known; otherwise null.</param>
public readonly record struct UpdateDownloadProgress(UpdateDownloadPhase Phase, double? Fraction);

/// <summary>Downloads a release's installer and proves it is the file the release published.</summary>
public interface IUpdateDownloader
{
    /// <summary>
    /// Downloads the installer into <c>%LOCALAPPDATA%\Porchlight\Updates</c> (clearing older files
    /// there first), verifies its SHA-256 against the release's <c>.sha256</c> file, and returns the
    /// installer's path. On any failure the file is deleted and a <see cref="SelfUpdateException"/> with
    /// a friendly message is thrown; cancellation throws <see cref="OperationCanceledException"/>
    /// (also after deleting the partial file).
    /// </summary>
    Task<string> DownloadAsync(ReleaseInfo release, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IUpdateDownloader"/>
/// <remarks>The <see cref="HttpMessageHandler"/> passed in must not follow redirects on its own
/// (<c>AllowAutoRedirect = false</c>): each hop is checked against <see cref="SelfUpdateUrlPolicy"/> here.</remarks>
public sealed partial class UpdateDownloader : IUpdateDownloader, IDisposable
{
    private const int MaxRedirects = 5;
    private const int BufferSize = 81_920;
    private const long MaxInstallerBytes = 500L * 1024 * 1024;
    private const int MaxChecksumBytes = 4096;

    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    private const string ConnectionMessage =
        "Couldn't download the update. Check your internet connection and try again.";

    private readonly HttpClient _httpClient;
    private readonly IRunningAppInfo _appInfo;
    private readonly IInstallerSignatureVerifier _signatureVerifier;
    private readonly ILogger<UpdateDownloader> _logger;
    private readonly string _updatesDirectory;
    private readonly bool _requireSignature;

    public UpdateDownloader(
        HttpMessageHandler handler, IRunningAppInfo appInfo, IInstallerSignatureVerifier signatureVerifier,
        ILogger<UpdateDownloader> logger, string? updatesDirectory = null,
        bool requireSignature = SelfUpdatePolicy.RequireSignedInstaller)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _appInfo = appInfo;
        _signatureVerifier = signatureVerifier;
        _logger = logger;
        _updatesDirectory = updatesDirectory ?? DefaultUpdatesDirectory;
        _requireSignature = requireSignature;
    }

    /// <summary><c>%LOCALAPPDATA%\Porchlight\Updates</c>.</summary>
    public static string DefaultUpdatesDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Porchlight", "Updates");

    public async Task<string> DownloadAsync(
        ReleaseInfo release, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (release.InstallerUrl is null || release.ChecksumUrl is null
            || !SelfUpdateUrlPolicy.IsAllowedDownloadUrl(release.InstallerUrl)
            || !SelfUpdateUrlPolicy.IsAllowedDownloadUrl(release.ChecksumUrl))
        {
            LogNoAllowedAssets(release.Tag);
            throw new SelfUpdateException(
                "This release can't be installed automatically. Use the release page to download it.");
        }

        PrepareDirectory();
        var finalPath = Path.Combine(_updatesDirectory, release.InstallerFileName);
        var partialPath = finalPath + ".download";

        try
        {
            var expectedHash = await FetchExpectedHashAsync(release, cancellationToken).ConfigureAwait(false);
            var actualHash = await DownloadFileAsync(release.InstallerUrl, partialPath, progress, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new UpdateDownloadProgress(UpdateDownloadPhase.Verifying, null));
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                LogHashMismatch(release.Tag);
                throw new SelfUpdateException(
                    "The downloaded update didn't match its checksum, so it was discarded. Please try again later.");
            }

            if (_requireSignature && !_signatureVerifier.HasValidSignature(partialPath))
            {
                LogBadSignature(release.Tag);
                throw new SelfUpdateException(
                    "The downloaded update isn't properly signed, so it was discarded.");
            }

            File.Move(partialPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch (Exception ex)
        {
            TryDelete(partialPath);
            TryDelete(finalPath);
            if (ex is OperationCanceledException or SelfUpdateException)
            {
                throw;
            }

            if (ex is HttpRequestException)
            {
                LogDownloadFailed(ex.Message);
                throw new SelfUpdateException(ConnectionMessage, ex);
            }

            if (ex is IOException or UnauthorizedAccessException)
            {
                LogSaveFailed(ex);
                throw new SelfUpdateException("Couldn't save the update to this PC's disk. Free up some space and try again.", ex);
            }

            throw;
        }
    }

    private async Task<string> FetchExpectedHashAsync(ReleaseInfo release, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HeaderTimeout);
        try
        {
            using var response = await SendWithRedirectsAsync(release.ChecksumUrl!, timeout.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > MaxChecksumBytes)
            {
                throw new SelfUpdateException("The update's checksum file wasn't in the expected format, so the update was cancelled.");
            }

            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!ChecksumFile.TryParse(text, release.InstallerFileName, out var hash))
            {
                throw new SelfUpdateException("The update's checksum file wasn't in the expected format, so the update was cancelled.");
            }

            return hash;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SelfUpdateException(ConnectionMessage);
        }
    }

    private async Task<string> DownloadFileAsync(
        Uri url, string destination, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(HeaderTimeout);
        try
        {
            using var response = await SendWithRedirectsAsync(url, stall.Token).ConfigureAwait(false);
            var total = response.Content.Headers.ContentLength;
            if (total > MaxInstallerBytes)
            {
                throw new SelfUpdateException("The update file is larger than expected, so the update was cancelled.");
            }

            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var source = await response.Content.ReadAsStreamAsync(stall.Token).ConfigureAwait(false);
            await using var target = new FileStream(
                destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

            var buffer = new byte[BufferSize];
            long received = 0;
            var lastPercent = -1;
            progress?.Report(new UpdateDownloadProgress(UpdateDownloadPhase.Downloading, total is > 0 ? 0 : null));

            while (true)
            {
                stall.CancelAfter(StallTimeout);
                var read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                received += read;
                if (received > MaxInstallerBytes)
                {
                    throw new SelfUpdateException("The update file is larger than expected, so the update was cancelled.");
                }

                hasher.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), stall.Token).ConfigureAwait(false);

                if (total is > 0)
                {
                    var percent = (int)(received * 100 / total.Value);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(new UpdateDownloadProgress(
                            UpdateDownloadPhase.Downloading, Math.Min(1.0, (double)received / total.Value)));
                    }
                }
            }

            if (total is > 0 && received != total)
            {
                throw new SelfUpdateException(ConnectionMessage);
            }

            return Convert.ToHexStringLower(hasher.GetHashAndReset());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SelfUpdateException(ConnectionMessage);
        }
    }

    /// <summary>GETs <paramref name="url"/>, following redirects by hand so every hop is re-checked
    /// against <see cref="SelfUpdateUrlPolicy"/> (GitHub redirects release assets to its CDN hosts).</summary>
    private async Task<HttpResponseMessage> SendWithRedirectsAsync(Uri url, CancellationToken cancellationToken)
    {
        var current = url;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!SelfUpdateUrlPolicy.IsAllowedDownloadUrl(current))
            {
                LogRefusedHost(current.Host);
                throw new SelfUpdateException(
                    "The update pointed to an address Porchlight doesn't trust, so it was cancelled.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Porchlight", _appInfo.Version.ToString(3)));
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                response.Dispose();
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                LogHttpStatus(status);
                throw new SelfUpdateException(
                    status == (int)HttpStatusCode.NotFound
                        ? "The update file could not be found on GitHub. Please try again later."
                        : ConnectionMessage);
            }

            return response;
        }

        throw new SelfUpdateException(ConnectionMessage);
    }

    /// <summary>Creates the updates folder and removes everything older in it.</summary>
    private void PrepareDirectory()
    {
        try
        {
            Directory.CreateDirectory(_updatesDirectory);
            foreach (var file in Directory.EnumerateFiles(_updatesDirectory))
            {
                TryDelete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPrepareFailed(ex, _updatesDirectory);
            throw new SelfUpdateException("Couldn't prepare a place to save the update on this PC.", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a locked leftover is cleared by the next update attempt.
        }
    }

    public void Dispose() => _httpClient.Dispose();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Release {Tag} has no installer/checksum at an allowed download address.")]
    private partial void LogNoAllowedAssets(string tag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloaded installer for {Tag} failed its SHA-256 check.")]
    private partial void LogHashMismatch(string tag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloaded installer for {Tag} has no valid signature.")]
    private partial void LogBadSignature(string tag);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update download failed: {Reason}")]
    private partial void LogDownloadFailed(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't save the update download.")]
    private partial void LogSaveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refusing to download an update from {Host}.")]
    private partial void LogRefusedHost(string host);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update download got HTTP {StatusCode}.")]
    private partial void LogHttpStatus(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't prepare the updates folder {Folder}.")]
    private partial void LogPrepareFailed(Exception exception, string folder);
}
