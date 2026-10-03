namespace Porchlight.Core.Safety;

/// <summary>Whether a security product is currently protecting the PC.</summary>
public enum ProductRunState
{
    /// <summary>Switched off.</summary>
    Off,

    /// <summary>Running and protecting.</summary>
    On,

    /// <summary>Paused for a while.</summary>
    Snoozed,

    /// <summary>The subscription or license has run out.</summary>
    Expired,

    /// <summary>The state value was not one we recognise.</summary>
    Unknown,
}
