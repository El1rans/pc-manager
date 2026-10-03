namespace Porchlight.Core.Safety;

/// <summary>Decodes the Security Center <c>productState</c> bitfield (for example 266240 = 0x41000 is
/// "on, up to date"). Bits 12-15 hold the state, bits 4-7 the definitions status.</summary>
public static class ProductStateParser
{
    private const int StateShift = 12;
    private const int DefinitionsShift = 4;
    private const uint NibbleMask = 0xF;

    private const uint StateOff = 0;
    private const uint StateOn = 1;
    private const uint StateSnoozed = 2;
    private const uint StateExpired = 3;
    private const uint DefinitionsCurrent = 0;
    private const uint DefinitionsOutOfDate = 1;

    public static ProductStateInfo Parse(long productState)
    {
        var bits = unchecked((uint)productState);
        var state = ((bits >> StateShift) & NibbleMask) switch
        {
            StateOff => ProductRunState.Off,
            StateOn => ProductRunState.On,
            StateSnoozed => ProductRunState.Snoozed,
            StateExpired => ProductRunState.Expired,
            _ => ProductRunState.Unknown,
        };

        bool? upToDate = ((bits >> DefinitionsShift) & NibbleMask) switch
        {
            DefinitionsCurrent => true,
            DefinitionsOutOfDate => false,
            _ => null,
        };

        return new ProductStateInfo(state, upToDate);
    }
}
