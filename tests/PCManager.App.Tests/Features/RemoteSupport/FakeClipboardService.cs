using PCManager.App.Features.RemoteSupport;

namespace PCManager.App.Tests.Features.RemoteSupport;

internal sealed class FakeClipboardService : IClipboardService
{
    public List<string> Texts { get; } = [];

    public string? LastText => Texts.Count > 0 ? Texts[^1] : null;

    public void SetText(string text) => Texts.Add(text);
}
