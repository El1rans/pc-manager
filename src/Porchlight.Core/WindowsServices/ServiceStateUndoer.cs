using Porchlight.Core.Changes;

namespace Porchlight.Core.WindowsServices;

/// <summary>Undoes "started"/"stopped" a service by doing the opposite.</summary>
public sealed class ServiceStateUndoer(IWindowsServicesService services) : IChangeUndoer
{
    public const string Type = "services.state";

    /// <summary>What to restore: the service and whether it should be running.</summary>
    public sealed record Payload(string Name, string DisplayName, bool Running);

    public string UndoType => Type;

    public static string CreatePayload(string name, string displayName, bool runningBefore) =>
        ChangeUndoPayload.Serialize(new Payload(name, displayName, runningBefore));

    public async Task<ChangeUndoResult> UndoAsync(string payload, CancellationToken cancellationToken)
    {
        var data = ChangeUndoPayload.TryDeserialize<Payload>(payload);
        if (data is null)
        {
            return ChangeUndoResult.Fail("This change can't be undone.");
        }

        await services.ListAsync(cancellationToken).ConfigureAwait(false);
        var outcome = data.Running
            ? await services.StartAsync(data.Name, cancellationToken).ConfigureAwait(false)
            : await services.StopAsync(data.Name, cancellationToken).ConfigureAwait(false);
        return ServiceUndoMessages.From(outcome, $"{data.DisplayName} is {(data.Running ? "running" : "stopped")} again.");
    }
}
