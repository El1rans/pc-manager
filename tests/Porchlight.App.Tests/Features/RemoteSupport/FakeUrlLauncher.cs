using Porchlight.App.Features.RemoteSupport;

namespace Porchlight.App.Tests.Features.RemoteSupport;

internal sealed class FakeUrlLauncher : IUrlLauncher
{
    public List<string> OpenedUrls { get; } = [];

    public void Open(string url) => OpenedUrls.Add(url);
}
