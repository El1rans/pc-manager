using Porchlight.Core.Safety;

namespace Porchlight.App.Features.Safety;

/// <summary>Segoe Fluent Icons glyphs for a <see cref="SafetyLevel"/>; always shown next to text.</summary>
internal static class SafetyGlyphs
{
    private const string Check = "";
    private const string Warning = "";
    private const string Question = "";

    public static string For(SafetyLevel level) => level switch
    {
        SafetyLevel.Good => Check,
        SafetyLevel.Attention => Warning,
        _ => Question,
    };
}
