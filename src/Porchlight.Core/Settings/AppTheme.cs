using System.Text.Json.Serialization;

namespace Porchlight.Core.Settings;

/// <summary>Which colour theme the app uses. Stored by name so a settings file stays readable.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppTheme
{
    /// <summary>Follow the Windows light/dark setting (the default).</summary>
    System,

    /// <summary>Always use the light theme.</summary>
    Light,

    /// <summary>Always use the dark theme.</summary>
    Dark,
}
