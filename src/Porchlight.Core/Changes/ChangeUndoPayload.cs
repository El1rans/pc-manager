using System.Text.Json;

namespace Porchlight.Core.Changes;

/// <summary>Small helpers so features store and read their undo payloads as JSON the same way.</summary>
public static class ChangeUndoPayload
{
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    /// <summary>The payload as <typeparamref name="T"/>, or null when it is missing or unreadable.</summary>
    public static T? TryDeserialize<T>(string? payload)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payload);
        }
        catch (JsonException)
        {
            // A payload we can't read just means this entry can't be undone; the caller says so.
            return null;
        }
    }
}
