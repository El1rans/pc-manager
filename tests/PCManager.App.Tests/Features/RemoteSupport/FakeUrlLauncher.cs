using PCManager.App.Features.RemoteSupport;

namespace PCManager.App.Tests.Features.RemoteSupport;

internal sealed class FakeUrlLauncher : IUrlLauncher
{
    public List<string> OpenedUrls { get; } = [];

    public void Open(string url) => OpenedUrls.Add(url);
}
