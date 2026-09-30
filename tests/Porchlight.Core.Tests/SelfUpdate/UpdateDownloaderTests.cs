using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public sealed class UpdateDownloaderTests : IDisposable
{
    private const string Name = "Porchlight-Setup-0.2.0.exe";
    private const string InstallerUrl = "https://github.com/El1rans/porchlight/releases/download/v0.2.0/" + Name;
    private const string ChecksumUrl = InstallerUrl + ".sha256";

    private static readonly byte[] InstallerBytes = Encoding.ASCII.GetBytes(new string('x', 200_000));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PorchlightCoreTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeAppInfo : IRunningAppInfo
    {
        public Version Version { get; } = new(0, 1, 0);

        public string? ExecutableDirectory => null;
    }

    private sealed class FakeVerifier(bool valid) : IInstallerSignatureVerifier
    {
        public int Calls { get; private set; }

        public bool HasValidSignature(string path)
        {
            Calls++;
            return valid;
        }
    }

    private sealed class FakeHandler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!);
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static ReleaseInfo Release(string installerUrl = InstallerUrl, string checksumUrl = ChecksumUrl) =>
        new(new Version(0, 2, 0), "v0.2.0", new Uri("https://github.com/El1rans/porchlight/releases/tag/v0.2.0"), null,
            new Uri(installerUrl), new Uri(checksumUrl));

    /// <summary>Serves the installer and a checksum file; overrides let a test corrupt either.</summary>
    private static FakeHandler Serve(
        byte[]? installer = null, string? checksumText = null, Func<Uri, HttpResponseMessage?>? custom = null)
    {
        installer ??= InstallerBytes;
        checksumText ??= $"{Sha256(installer)} *{Name}";
        return new FakeHandler(uri =>
        {
            var special = custom?.Invoke(uri);
            if (special is not null)
            {
                return special;
            }

            return uri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(checksumText) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) };
        });
    }

    private UpdateDownloader Create(FakeHandler handler, bool requireSignature = false, FakeVerifier? verifier = null) =>
        new(handler, new FakeAppInfo(), verifier ?? new FakeVerifier(true), NullLogger<UpdateDownloader>.Instance,
            _directory, requireSignature);

    [Fact]
    public async Task Download_MatchingHash_ReturnsVerifiedFileInUpdatesFolder()
    {
        using var downloader = Create(Serve());

        var path = await downloader.DownloadAsync(Release(), null, CancellationToken.None);

        Assert.Equal(Path.Combine(_directory, Name), path);
        Assert.Equal(InstallerBytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Download_UpperCaseHashInChecksumFile_StillMatches()
    {
        using var downloader = Create(Serve(checksumText: Sha256(InstallerBytes).ToUpperInvariant() + "  " + Name));

        var path = await downloader.DownloadAsync(Release(), null, CancellationToken.None);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Download_ClearsOlderFilesFromTheUpdatesFolder()
    {
        Directory.CreateDirectory(_directory);
        var stale = Path.Combine(_directory, "Porchlight-Setup-0.0.1.exe");
        await File.WriteAllTextAsync(stale, "old", TestContext.Current.CancellationToken);
        using var downloader = Create(Serve());

        await downloader.DownloadAsync(Release(), null, CancellationToken.None);

        Assert.False(File.Exists(stale));
    }

    [Fact]
    public async Task Download_HashMismatch_DeletesFileAndThrowsFriendlyError()
    {
        var wrong = new string('0', 64);
        using var downloader = Create(Serve(checksumText: $"{wrong} *{Name}"));

        var error = await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(), null, CancellationToken.None));

        Assert.Contains("checksum", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("0123 *Porchlight-Setup-0.2.0.exe")]
    public async Task Download_BadChecksumFile_ThrowsAndLeavesNoFile(string checksumText)
    {
        var handler = Serve(checksumText: checksumText);
        using var downloader = Create(handler);

        await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(), null, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_directory));
        Assert.DoesNotContain(handler.Requested, u => u.AbsolutePath.EndsWith(".exe", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://evil.example/Porchlight-Setup-0.2.0.exe", ChecksumUrl)]
    [InlineData(InstallerUrl, "https://evil.example/x.sha256")]
    [InlineData("http://github.com/El1rans/porchlight/releases/download/v0.2.0/Porchlight-Setup-0.2.0.exe", ChecksumUrl)]
    public async Task Download_DisallowedUrl_IsRefusedBeforeAnyRequest(string installerUrl, string checksumUrl)
    {
        var handler = Serve();
        using var downloader = Create(handler);

        await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(installerUrl, checksumUrl), null, CancellationToken.None));

        Assert.Empty(handler.Requested);
    }

    [Fact]
    public async Task Download_ReleaseWithoutAssets_IsRefused()
    {
        var release = new ReleaseInfo(new Version(0, 2, 0), "v0.2.0", new Uri("https://github.com/x"), null, null, null);
        using var downloader = Create(Serve());

        await Assert.ThrowsAsync<SelfUpdateException>(() => downloader.DownloadAsync(release, null, CancellationToken.None));
    }

    [Fact]
    public async Task Download_RedirectToGitHubCdn_IsFollowed()
    {
        var cdn = new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/1/2?sig=abc");
        var handler = Serve(custom: uri =>
        {
            if (uri.AbsoluteUri == InstallerUrl)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = cdn;
                return redirect;
            }

            return uri == cdn ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(InstallerBytes) } : null;
        });
        using var downloader = Create(handler);

        var path = await downloader.DownloadAsync(Release(), null, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Contains(cdn, handler.Requested);
    }

    [Theory]
    [InlineData("https://evil.example/x.exe")]
    [InlineData("http://objects.githubusercontent.com/x.exe")]
    public async Task Download_RedirectToDisallowedHost_IsRefusedAndNothingIsFetchedFromIt(string target)
    {
        var handler = Serve(custom: uri =>
        {
            if (uri.AbsoluteUri == InstallerUrl)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri(target);
                return redirect;
            }

            return null;
        });
        using var downloader = Create(handler);

        await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(), null, CancellationToken.None));

        Assert.DoesNotContain(handler.Requested, u => u.AbsoluteUri == target);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Download_NotFound_ThrowsFriendlyError()
    {
        var handler = Serve(custom: uri => uri.AbsoluteUri == InstallerUrl ? new HttpResponseMessage(HttpStatusCode.NotFound) : null);
        using var downloader = Create(handler);

        await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(), null, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Download_NetworkError_ThrowsFriendlyError()
    {
        using var downloader = Create(new FakeHandler(_ => throw new HttpRequestException("offline")));

        var error = await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(), null, CancellationToken.None));

        Assert.Contains("internet", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_Cancelled_DeletesPartialFile()
    {
        using var cts = new CancellationTokenSource();
        var handler = Serve(custom: uri =>
        {
            if (uri.AbsolutePath.EndsWith(".exe", StringComparison.Ordinal))
            {
                cts.Cancel();
            }

            return null;
        });
        using var downloader = Create(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => downloader.DownloadAsync(Release(), null, cts.Token));

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Download_ReportsDownloadingThenVerifying()
    {
        var reports = new List<UpdateDownloadProgress>();
        using var downloader = Create(Serve());

        await downloader.DownloadAsync(Release(), new SyncProgress(reports), CancellationToken.None);

        Assert.Equal(UpdateDownloadPhase.Verifying, reports[^1].Phase);
        Assert.Contains(reports, r => r.Phase == UpdateDownloadPhase.Downloading && r.Fraction == 1.0);
        Assert.All(reports.Where(r => r.Phase == UpdateDownloadPhase.Downloading), r => Assert.InRange(r.Fraction ?? 0, 0, 1));
    }

    [Fact]
    public async Task Download_SignatureNotRequired_DoesNotConsultVerifier()
    {
        var verifier = new FakeVerifier(valid: false);
        using var downloader = Create(Serve(), requireSignature: false, verifier: verifier);

        await downloader.DownloadAsync(Release(), null, CancellationToken.None);

        Assert.Equal(0, verifier.Calls);
    }

    [Fact]
    public async Task Download_SignatureRequiredAndInvalid_DeletesFileAndThrows()
    {
        var verifier = new FakeVerifier(valid: false);
        using var downloader = Create(Serve(), requireSignature: true, verifier: verifier);

        await Assert.ThrowsAsync<SelfUpdateException>(
            () => downloader.DownloadAsync(Release(), null, CancellationToken.None));

        Assert.Equal(1, verifier.Calls);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Download_SignatureRequiredAndValid_Succeeds()
    {
        using var downloader = Create(Serve(), requireSignature: true, verifier: new FakeVerifier(valid: true));

        var path = await downloader.DownloadAsync(Release(), null, CancellationToken.None);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SignaturePolicy_IsOffWhileReleasesAreUnsigned() =>
        Assert.False(SelfUpdatePolicy.RequireSignedInstaller);

    /// <summary>Reports synchronously (unlike <see cref="Progress{T}"/>, which posts), so assertions can rely on order.</summary>
    private sealed class SyncProgress(List<UpdateDownloadProgress> reports) : IProgress<UpdateDownloadProgress>
    {
        public void Report(UpdateDownloadProgress value) => reports.Add(value);
    }
}
