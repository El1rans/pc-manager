using Porchlight.App.Features.RemoteSupport;

namespace Porchlight.App.Tests.Features.RemoteSupport;

internal sealed class FakeWindowsVersionReader : IWindowsVersionReader
{
    public string Version { get; set; } = "Windows 11 Pro (build 26200)";

    public string GetFriendlyVersion() => Version;
}
