using Porchlight.Core.Safety;

namespace Porchlight.App.Features.Safety;

/// <summary>One remote-control tool in the "Who can connect to this PC?" card.</summary>
public sealed class RemoteToolRowViewModel(RemoteToolFinding finding)
{
    public string Name => finding.Name;

    public string StatusText => finding.StatusText;

    /// <summary>"Set up by Porchlight" for Porchlight's own AnyDesk, otherwise the calm warning.</summary>
    public string Note => finding.SetUpByPorchlight ? RemoteAccessStatus.PorchlightLabel : RemoteAccessStatus.UnknownToolWarning;

    public string Glyph => finding.SetUpByPorchlight ? SafetyGlyphs.For(SafetyLevel.Good) : SafetyGlyphs.For(SafetyLevel.Attention);

    public string AutomationName => $"{finding.Name}: {finding.StatusText}. {Note}";
}
