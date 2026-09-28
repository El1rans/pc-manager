using Porchlight.Core.Processes;

namespace Porchlight.App.Tests.Features.Updates;

/// <summary>Fake <see cref="IAppInUseDiagnosticsService"/> for <see cref="UpdatesViewModelTests"/> -
/// never touches the real registry or Restart Manager.</summary>
internal sealed class FakeAppInUseDiagnosticsService : IAppInUseDiagnosticsService
{
    /// <summary>What <see cref="TryDescribeLockingProcesses"/> returns - null (the default) means
    /// "couldn't determine anything", same as the real implementation's failure/not-found case.</summary>
    public string? Result { get; set; }

    public List<(string PackageId, string DisplayName)> Calls { get; } = [];

    public Task<string?> TryDescribeLockingProcessesAsync(string packageId, string displayName)
    {
        Calls.Add((packageId, displayName));
        return Task.FromResult(Result);
    }
}
