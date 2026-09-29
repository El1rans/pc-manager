using Porchlight.Core.Network;

namespace Porchlight.App.Features.Network;

/// <summary>A suggested fix shown as a button with a plain-language explanation.</summary>
public sealed class RemedyViewModel
{
    public RemedyViewModel(RemedyKind kind)
    {
        Kind = kind;
        (Title, Description) = kind switch
        {
            RemedyKind.FlushDns => ("Clear saved website addresses",
                "Quick and safe. Fixes many \"page can't be found\" problems."),
            RemedyKind.RenewIp => ("Get a fresh connection",
                "Asks your router for a new address. Your internet drops for a few seconds."),
            RemedyKind.ResetAdapter => ("Switch the network connection off and on",
                "Like unplugging and replugging it. Needs administrator rights."),
            _ => ("Open Windows network settings",
                "A last resort. Windows has a \"Network reset\" there; Porchlight will not run it for you."),
        };
    }

    public RemedyKind Kind { get; }

    public string Title { get; }

    public string Description { get; }
}
