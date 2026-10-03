using Porchlight.Core.Changes;

namespace Porchlight.Core.Startup;

/// <summary>Undoes "turned a startup app on/off" by putting it back the way it was.</summary>
public sealed class StartupChangeUndoer(IStartupService startup) : IChangeUndoer
{
    public const string Type = "startup.enabled";

    /// <summary>What to restore: the entry and whether it should be on.</summary>
    public sealed record Payload(string EntryId, string Name, bool Enabled);

    public string UndoType => Type;

    /// <summary>Payload text for a change that left <paramref name="enabledBefore"/> as the old state.</summary>
    public static string CreatePayload(string entryId, string name, bool enabledBefore) =>
        ChangeUndoPayload.Serialize(new Payload(entryId, name, enabledBefore));

    public async Task<ChangeUndoResult> UndoAsync(string payload, CancellationToken cancellationToken)
    {
        var data = ChangeUndoPayload.TryDeserialize<Payload>(payload);
        if (data is null)
        {
            return ChangeUndoResult.Fail("This change can't be undone.");
        }

        // The service only accepts ids from its last listing, so list first (a new session has none).
        await startup.ListAsync(cancellationToken).ConfigureAwait(false);
        var result = await startup.SetEnabledAsync(data.EntryId, data.Enabled, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            StartupChangeResult.Changed => ChangeUndoResult.Ok(
                $"{data.Name} will {(data.Enabled ? "start" : "no longer start")} when you sign in again."),
            StartupChangeResult.NotFound => ChangeUndoResult.Fail($"{data.Name} isn't in the startup list any more."),
            StartupChangeResult.NeedsAdmin => ChangeUndoResult.Fail(
                "Changing this one needs administrator rights. Use Restart as administrator, then try again."),
            _ => ChangeUndoResult.Fail("Couldn't undo this. Try again, or ask for help."),
        };
    }
}
