using Porchlight.Core.Components;

namespace Porchlight.App.Tests.Features.Setup;

internal sealed class FakeRegistryReader : IRegistryReader
{
    public IReadOnlyList<string> InstallerHandledComponentIds { get; set; } = [];

    public UninstallEntry? FindUninstallEntry(string displayNameContains) => null;

    public bool ServiceExists(string serviceName) => false;

    public IReadOnlyList<string> GetInstallerHandledComponentIds() => InstallerHandledComponentIds;

    public int? GetCurrentUserDwordValue(string subKey, string valueName) => null;
}
