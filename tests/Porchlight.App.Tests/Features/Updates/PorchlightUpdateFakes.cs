using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Features.Updates;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.Core.SelfUpdate;

namespace Porchlight.App.Tests.Features.Updates;

/// <summary>Fakes for the self-update pieces, so no test touches the network, disk or a real installer.</summary>
internal sealed class FakeReleaseChecker : IReleaseChecker
{
    public SelfUpdateCheckResult Result { get; set; } = SelfUpdateCheckResult.UpToDate;

    public int Calls { get; private set; }

    public Task<SelfUpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeUpdateDownloader : IUpdateDownloader
{
    public string Path { get; set; } = "C:\\fake\\Porchlight-Setup-0.2.0.exe";

    public Exception? Failure { get; set; }

    public int Calls { get; private set; }

    public Task<string> DownloadAsync(ReleaseInfo release, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        Calls++;
        return Failure is null ? Task.FromResult(Path) : Task.FromException<string>(Failure);
    }
}

internal sealed class FakeInstallerLauncher : IInstallerLauncher
{
    public InstallerLaunchResult Result { get; set; } = InstallerLaunchResult.Started;

    public List<string> Launched { get; } = [];

    public InstallerLaunchResult Launch(string installerPath)
    {
        Launched.Add(installerPath);
        return Result;
    }
}

internal sealed class FakeInstallTypeDetector : IInstallTypeDetector
{
    public InstallType Type { get; set; } = InstallType.Installed;

    public InstallType Detect() => Type;
}

internal sealed class FakeRunningAppInfo : IRunningAppInfo
{
    public Version Version { get; set; } = new(0, 1, 0);

    public string? ExecutableDirectory { get; set; } = "C:\\Program Files\\Porchlight";
}

internal sealed class FakeAppLifetime : IAppLifetime
{
    public int ShutdownCalls { get; private set; }

    public void Shutdown(int exitCode = 0) => ShutdownCalls++;
}

/// <summary>Builds a <see cref="PorchlightUpdateViewModel"/> wired to the fakes above.</summary>
internal sealed class PorchlightUpdateHarness
{
    public FakeReleaseChecker Checker { get; } = new();

    public FakeUpdateDownloader Downloader { get; } = new();

    public FakeInstallerLauncher Launcher { get; } = new();

    public FakeInstallTypeDetector InstallType { get; } = new();

    public FakeRunningAppInfo AppInfo { get; } = new();

    public FakeUrlLauncher UrlLauncher { get; } = new();

    public FakeAppLifetime Lifetime { get; } = new();

    public PorchlightUpdateViewModel Create() => new(
        Checker, Downloader, Launcher, InstallType, AppInfo, UrlLauncher, Lifetime,
        NullLogger<PorchlightUpdateViewModel>.Instance);

    public static ReleaseInfo Release(string version = "0.2.0", bool withAssets = true) =>
        new(new Version(version), "v" + version, new Uri("https://github.com/El1rans/porchlight/releases/tag/v" + version),
            "notes",
            withAssets ? new Uri($"https://github.com/El1rans/porchlight/releases/download/v{version}/Porchlight-Setup-{version}.exe") : null,
            withAssets ? new Uri($"https://github.com/El1rans/porchlight/releases/download/v{version}/Porchlight-Setup-{version}.exe.sha256") : null);

    public void Offer(ReleaseInfo? release = null) =>
        Checker.Result = new SelfUpdateCheckResult(SelfUpdateCheckStatus.UpdateAvailable, release ?? Release());
}
