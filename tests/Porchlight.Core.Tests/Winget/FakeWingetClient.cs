using Porchlight.Core.Winget;

namespace Porchlight.Core.Tests.Winget;

/// <summary>Fake <see cref="IWingetClient"/> for <see cref="ReinstallWorkflowTests"/> - never runs a
/// real <c>winget</c> process.</summary>
internal sealed class FakeWingetClient : IWingetClient
{
    public WingetResult UninstallResult { get; set; } = new(0, []);

    public WingetResult InstallResult { get; set; } = new(0, []);

    public List<string> Calls { get; } = [];

    public Task<IReadOnlyList<WingetPackage>> GetUpgradesAsync(
        bool includeUnknown, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WingetPackage>>([]);

    public Task<WingetResult> UpgradeAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(new WingetResult(0, []));

    public Task<WingetResult> ShowAsync(string id, IProgress<string>? log, CancellationToken cancellationToken) =>
        Task.FromResult(new WingetResult(0, []));

    public Task<WingetResult> UninstallAsync(string id, bool silent, IProgress<string>? log, CancellationToken cancellationToken)
    {
        if (UninstallException is { } ex)
        {
            return Task.FromException<WingetResult>(ex);
        }

        Calls.Add($"uninstall:{id}:{silent}");
        log?.Report($"> winget uninstall --id {id}");
        return Task.FromResult(UninstallResult);
    }

    public Task<WingetResult> InstallAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Calls.Add($"install:{id}:{silent}");
        log?.Report($"> winget install --id {id}");
        return Task.FromResult(InstallResult);
    }

    public Task<WingetResult> ExportAsync(
        string filePath, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(new WingetResult(0, []));

    public Task<IReadOnlyList<WingetSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WingetSearchResult>>([]);

    public List<WingetInstalledPackage> InstalledPackages { get; } = [];

    /// <summary>When set, <see cref="ListInstalledAsync"/> throws it.</summary>
    public Exception? ListException { get; set; }

    /// <summary>When set, <see cref="UninstallAsync"/> throws it.</summary>
    public Exception? UninstallException { get; set; }

    public Task<IReadOnlyList<WingetInstalledPackage>> ListInstalledAsync(CancellationToken cancellationToken) =>
        ListException is { } ex
            ? Task.FromException<IReadOnlyList<WingetInstalledPackage>>(ex)
            : Task.FromResult<IReadOnlyList<WingetInstalledPackage>>([.. InstalledPackages]);

    public Task<IReadOnlySet<string>> ListInstalledIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    public Task<WingetResult> ImportAsync(
        string filePath, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(new WingetResult(0, []));
}
