namespace Porchlight.Core.Safety;

/// <summary>How a safety check came out. Always shown with text and an icon, never colour alone.</summary>
public enum SafetyLevel
{
    /// <summary>Nothing to worry about.</summary>
    Good,

    /// <summary>Something the user (or their helper) should look at.</summary>
    Attention,

    /// <summary>The check could not be done on this PC.</summary>
    Unknown,
}
