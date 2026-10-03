using System.Text.Json.Serialization;

namespace Porchlight.Core.Changes;

/// <summary>One thing Porchlight changed on this PC, as kept in the change journal.</summary>
/// <param name="Id">Unique id; what <see cref="IChangeJournal.UndoAsync"/> takes.</param>
/// <param name="Time">When the change happened.</param>
/// <param name="Area">The part of Porchlight that made it.</param>
/// <param name="Description">One plain sentence, e.g. "Turned off Spotify at startup".</param>
/// <param name="UndoType">Key of the <see cref="IChangeUndoer"/> that can reverse it; null when it can't be undone.</param>
/// <param name="UndoPayload">Whatever that undoer needs (usually small JSON); null with no undo.</param>
/// <param name="UndoneAt">When it was undone, or null.</param>
public sealed record ChangeEntry(
    Guid Id,
    DateTimeOffset Time,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ChangeArea Area,
    string Description,
    string? UndoType,
    string? UndoPayload,
    DateTimeOffset? UndoneAt)
{
    /// <summary>True while an undoer exists for this entry and it has not been undone yet.</summary>
    [JsonIgnore]
    public bool CanUndo => UndoType is not null && UndoneAt is null;
}
