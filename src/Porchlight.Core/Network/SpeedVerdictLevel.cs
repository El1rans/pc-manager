namespace Porchlight.Core.Network;

/// <summary>How good a measured speed is, for pairing an icon and word with the numbers.</summary>
public enum SpeedVerdictLevel
{
    /// <summary>Nothing could be measured.</summary>
    Unknown,

    /// <summary>Very slow.</summary>
    Poor,

    /// <summary>Usable for the basics.</summary>
    Fair,

    /// <summary>Good for everyday use including video calls.</summary>
    Good,

    /// <summary>Plenty for a whole household.</summary>
    Excellent,
}
