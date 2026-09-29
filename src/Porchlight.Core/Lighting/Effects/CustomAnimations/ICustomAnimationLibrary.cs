namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <summary>A summary of one imported animation, for the Lighting page's list and pickers.</summary>
/// <param name="Id">Stable key, derived from the animation's name - what
/// <see cref="EffectAssignment.Settings"/>' <c>"animationId"</c> refers to. Only ever lowercase
/// letters, digits and dashes (see <see cref="CustomAnimationLibrary.IsValidId"/>).</param>
public sealed record CustomAnimationInfo(
    string Id, string Name, string? Description, int FrameCount, TimeSpan Duration);

/// <summary>The outcome of an import: exactly one of <see cref="Imported"/> or
/// <see cref="Error"/> is set.</summary>
public sealed record CustomAnimationImportResult(CustomAnimationInfo? Imported, string? Error)
{
    public bool Succeeded => Imported is not null;
}

/// <summary>
/// The user's imported custom animations (see docs/custom-animations.md), stored as validated
/// <c>.json</c> files in Porchlight's own app-data folder so an animation keeps working after the
/// original download is moved or deleted. Importing an animation with the same name as an existing
/// one replaces it, so "fix it and upload again" just works.
/// </summary>
public interface ICustomAnimationLibrary
{
    /// <summary>Every valid imported animation, sorted by name. A file that no longer parses (e.g.
    /// edited by hand) is skipped, not an error.</summary>
    IReadOnlyList<CustomAnimationInfo> List();

    /// <summary>The animation with <paramref name="id"/>, or null if there is none (or it no longer
    /// parses). Never throws.</summary>
    CustomAnimation? Load(string id);

    /// <summary>Validates and imports the animation file at <paramref name="path"/>. Never throws -
    /// a bad file, or one that couldn't be read, comes back as <see cref="CustomAnimationImportResult.Error"/>.</summary>
    CustomAnimationImportResult ImportFile(string path);

    /// <summary>Validates and imports an animation from its JSON text (e.g. pasted from an AI
    /// chat). Never throws.</summary>
    CustomAnimationImportResult ImportText(string json);

    /// <summary>Deletes the animation with <paramref name="id"/>. Returns false if there was none
    /// or it couldn't be deleted.</summary>
    bool Remove(string id);
}
