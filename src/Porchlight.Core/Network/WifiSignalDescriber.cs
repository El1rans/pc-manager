namespace Porchlight.Core.Network;

/// <summary>Turns Windows' 0-100 signal quality into bars and a plain word.</summary>
public static class WifiSignalDescriber
{
    /// <summary>Highest bar count.</summary>
    public const int MaxBars = 4;

    private const int ExcellentMinimum = 75;
    private const int GoodMinimum = 50;
    private const int FairMinimum = 25;

    /// <summary>Describes <paramref name="signalPercent"/> (values outside 0-100 are clamped).</summary>
    public static WifiSignal Describe(int signalPercent)
    {
        var percent = Math.Clamp(signalPercent, 0, 100);
        if (percent >= ExcellentMinimum)
        {
            return new WifiSignal(4, "Excellent");
        }

        if (percent >= GoodMinimum)
        {
            return new WifiSignal(3, "Good");
        }

        if (percent >= FairMinimum)
        {
            return new WifiSignal(2, "Fair");
        }

        return percent > 0 ? new WifiSignal(1, "Weak") : new WifiSignal(0, "No signal");
    }
}
