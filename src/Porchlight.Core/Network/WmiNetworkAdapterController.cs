using System.Management;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkAdapterController"/>
/// <remarks>Calls <c>Disable()</c> / <c>Enable()</c> on the <c>MSFT_NetAdapter</c> instance in
/// <c>root\StandardCimv2</c> whose <c>InterfaceGuid</c> matches. Needs administrator rights.</remarks>
public sealed class WmiNetworkAdapterController : INetworkAdapterController
{
    private const string Scope = @"root\StandardCimv2";
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(20);

    public Task<bool> DisableAsync(string adapterId, CancellationToken cancellationToken) =>
        InvokeAsync(adapterId, "Disable", cancellationToken);

    public Task<bool> EnableAsync(string adapterId, CancellationToken cancellationToken) =>
        InvokeAsync(adapterId, "Enable", cancellationToken);

    private static async Task<bool> InvokeAsync(string adapterId, string method, CancellationToken cancellationToken)
    {
        // Only a well-formed GUID is ever placed in the WQL text.
        if (!Guid.TryParse(adapterId, out var guid))
        {
            return false;
        }

        var call = Task.Run(() => Invoke(guid, method), CancellationToken.None);
        try
        {
            return await call.WaitAsync(CallTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Surface as a plain failure; the caller still runs Enable afterwards.
            return false;
        }
    }

    private static bool Invoke(Guid guid, string method)
    {
        var scope = new ManagementScope(Scope, new ConnectionOptions { Timeout = CallTimeout });
        scope.Connect();
        var query = new ObjectQuery($"SELECT * FROM MSFT_NetAdapter WHERE InterfaceGuid = '{guid:B}'");
        using var searcher = new ManagementObjectSearcher(scope, query, new System.Management.EnumerationOptions { Timeout = CallTimeout });
        foreach (var instance in searcher.Get().Cast<ManagementObject>())
        {
            using (instance)
            {
                using var result = instance.InvokeMethod(method, null, new InvokeMethodOptions { Timeout = CallTimeout });
                return result is null || Convert.ToUInt32(result["ReturnValue"] ?? 0U, System.Globalization.CultureInfo.InvariantCulture) == 0;
            }
        }

        return false;
    }
}
