namespace Porchlight.Core.Safety;

/// <summary>One antivirus or firewall product reported by Windows Security Center.</summary>
public sealed record SecurityProduct(string Name, SecurityProductKind Kind, ProductStateInfo State)
{
    /// <summary>Plain one-line description, e.g. "Microsoft Defender Antivirus - on, up to date".</summary>
    public string Detail => $"{Name} - {Describe()}";

    private string Describe()
    {
        var state = State.State switch
        {
            ProductRunState.On => "on",
            ProductRunState.Off => "off",
            ProductRunState.Snoozed => "paused",
            ProductRunState.Expired => "expired",
            _ => "status unknown",
        };

        if (Kind != SecurityProductKind.Antivirus || State.State != ProductRunState.On)
        {
            return state;
        }

        return State.DefinitionsUpToDate switch
        {
            true => "on, up to date",
            false => "on, out of date",
            _ => "on",
        };
    }
}
