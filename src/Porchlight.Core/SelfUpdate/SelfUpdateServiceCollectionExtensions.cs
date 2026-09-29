using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.SelfUpdate;

/// <summary>Registers the Core services behind in-app updating of Porchlight itself
/// (<c>docs/specs/23-self-update.md</c>).</summary>
public static class SelfUpdateServiceCollectionExtensions
{
    public static IServiceCollection AddSelfUpdateCore(this IServiceCollection services)
    {
        services.AddSingleton<IRunningAppInfo, RunningAppInfo>();
        services.AddSingleton<IInstallLocationReader, RegistryInstallLocationReader>();
        services.AddSingleton<IInstallTypeDetector, InstallTypeDetector>();
        services.AddSingleton<IInstallerSignatureVerifier, WinTrustSignatureVerifier>();
        services.AddSingleton<IInstallerLauncher, InstallerLauncher>();

        // Redirects are followed by hand (and every hop checked) by the downloader, so the
        // handlers must not follow them themselves.
        services.AddSingleton<IReleaseChecker>(sp => new GitHubReleaseChecker(
            new SocketsHttpHandler { AllowAutoRedirect = false },
            sp.GetRequiredService<IRunningAppInfo>(),
            sp.GetRequiredService<ILogger<GitHubReleaseChecker>>()));
        services.AddSingleton<IUpdateDownloader>(sp => new UpdateDownloader(
            new SocketsHttpHandler { AllowAutoRedirect = false },
            sp.GetRequiredService<IRunningAppInfo>(),
            sp.GetRequiredService<IInstallerSignatureVerifier>(),
            sp.GetRequiredService<ILogger<UpdateDownloader>>()));
        return services;
    }
}
