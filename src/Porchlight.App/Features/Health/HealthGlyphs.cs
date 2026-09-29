namespace Porchlight.App.Features.Health;

/// <summary>Segoe Fluent Icons glyphs used by the Health page.</summary>
internal static class HealthGlyphs
{
    public const string Ok = "";
    public const string Warning = "";
    public const string Unknown = "";

    public static string For(HealthSeverity severity) => severity switch
    {
        HealthSeverity.Ok => Ok,
        HealthSeverity.Warning => Warning,
        _ => Unknown,
    };
}
