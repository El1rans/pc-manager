using Porchlight.Core.Lighting.Effects.CustomAnimations;

namespace Porchlight.App.Tests.Features.Lighting;

/// <summary>In-memory <see cref="ICustomAnimationLibrary"/>: validates with the real
/// <see cref="CustomAnimationParser"/> but never touches the disk.</summary>
internal sealed class FakeCustomAnimationLibrary : ICustomAnimationLibrary
{
    private readonly Dictionary<string, CustomAnimation> _animations = new(StringComparer.Ordinal);

    /// <summary>Contents of the "files" <see cref="ImportFile"/> can read, by path.</summary>
    public Dictionary<string, string> Files { get; } = [];

    public IReadOnlyList<CustomAnimationInfo> List() =>
        [.. _animations.OrderBy(a => a.Value.Name, StringComparer.Ordinal).Select(a => ToInfo(a.Key, a.Value))];

    public CustomAnimation? Load(string id) => _animations.GetValueOrDefault(id);

    public CustomAnimationImportResult ImportFile(string path) =>
        Files.TryGetValue(path, out var text) ? ImportText(text) : new CustomAnimationImportResult(null, "That file doesn't exist.");

    public CustomAnimationImportResult ImportText(string json)
    {
        var parsed = CustomAnimationParser.Parse(json);
        if (parsed.Animation is not { } animation)
        {
            return new CustomAnimationImportResult(null, parsed.Error);
        }

        var id = CustomAnimationLibrary.IdFor(animation.Name);
        _animations[id] = animation;
        return new CustomAnimationImportResult(ToInfo(id, animation), null);
    }

    public bool Remove(string id) => _animations.Remove(id);

    private static CustomAnimationInfo ToInfo(string id, CustomAnimation animation) =>
        new(id, animation.Name, animation.Description, animation.Frames.Count, animation.TotalDuration);
}
