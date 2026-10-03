using Porchlight.Core.Changes;

namespace Porchlight.Core.WindowsServices;

/// <summary>Undoes a start-type change by setting the previous start type again.</summary>
public sealed class ServiceStartTypeUndoer(IWindowsServicesService services) : IChangeUndoer
{
    public const string Type = "services.starttype";

    /// <summary>What to restore: the service and its previous start type.</summary>
    public sealed record Payload(string Name, string DisplayName, ServiceStartType StartType);

    public string UndoType => Type;

    public static string CreatePayload(string name, string displayName, ServiceStartType previous) =>
        ChangeUndoPayload.Serialize(new Payload(name, displayName, previous));

    public async Task<ChangeUndoResult> UndoAsync(string payload, CancellationToken cancellationToken)
    {
        var data = ChangeUndoPayload.TryDeserialize<Payload>(payload);
        if (data is null)
        {
            return ChangeUndoResult.Fail("This change can't be undone.");
        }

        // The service only changes names from its last listing, so list first (a new session has none).
        await services.ListAsync(cancellationToken).ConfigureAwait(false);
        var outcome = await services.SetStartTypeAsync(data.Name, data.StartType, cancellationToken).ConfigureAwait(false);
        return ServiceUndoMessages.From(outcome, $"{data.DisplayName} is set to \"{ServiceClassifier.StartTypeLabel(data.StartType)}\" again.");
    }
}
