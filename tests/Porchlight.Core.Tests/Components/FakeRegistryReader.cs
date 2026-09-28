using Porchlight.Core.Components;

namespace Porchlight.Core.Tests.Components;

internal sealed class FakeRegistryReader : IRegistryReader
{
    private readonly Dictionary<string, UninstallEntry> _uninstallEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _services = new(StringComparer.OrdinalIgnoreCase);

    public void SetUninstallEntry(string displayNameContains, UninstallEntry entry) =>
        _uninstallEntries[displayNameContains] = entry;

    public void SetServiceExists(string serviceName) => _services.Add(serviceName);

    public UninstallEntry? FindUninstallEntry(string displayNameContains) =>
        _uninstallEntries.TryGetValue(displayNameContains, out var entry) ? entry : null;

    public bool ServiceExists(string serviceName) => _services.Contains(serviceName);

    public IReadOnlyList<string> InstallerHandledComponentIds { get; set; } = [];

    private readonly Dictionary<(string SubKey, string ValueName), int> _currentUserDwords = new();

    public IReadOnlyList<string> GetInstallerHandledComponentIds() => InstallerHandledComponentIds;

    public void SetCurrentUserDwordValue(string subKey, string valueName, int value) =>
        _currentUserDwords[(subKey, valueName)] = value;

    public int? GetCurrentUserDwordValue(string subKey, string valueName) =>
        _currentUserDwords.TryGetValue((subKey, valueName), out var value) ? value : null;
}
