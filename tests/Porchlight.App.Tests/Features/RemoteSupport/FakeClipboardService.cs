using Porchlight.App.Features.RemoteSupport;

namespace Porchlight.App.Tests.Features.RemoteSupport;

internal sealed class FakeClipboardService : IClipboardService
{
    public List<string> Texts { get; } = [];

    public string? LastText => Texts.Count > 0 ? Texts[^1] : null;

    /// <summary>When false, <see cref="SetText"/> reports failure without recording the text - for
    /// testing the "Couldn't copy" path (see <c>RemoteSupportViewModel.ShowCopyResult</c>).</summary>
    public bool NextResult { get; set; } = true;

    public bool SetText(string text)
    {
        if (!NextResult)
        {
            return false;
        }

        Texts.Add(text);
        return true;
    }
}
