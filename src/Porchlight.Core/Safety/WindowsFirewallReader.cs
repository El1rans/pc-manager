using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Safety;

/// <inheritdoc cref="IWindowsFirewallReader"/>
public sealed partial class WindowsFirewallReader(ILogger<WindowsFirewallReader> logger) : IWindowsFirewallReader
{
    private const string PolicyProgId = "HNetCfg.FwPolicy2";

    private static readonly WindowsFirewallProfile[] Profiles =
        [WindowsFirewallProfile.Domain, WindowsFirewallProfile.Private, WindowsFirewallProfile.Public];

    public async Task<WindowsFirewallStatus?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(Read, cancellationToken).WaitAsync(SafetyTimeouts.FirewallRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or RuntimeBinderException
            or TimeoutException or UnauthorizedAccessException or InvalidCastException)
        {
            LogFailed(ex);
            return null;
        }
    }

    private static WindowsFirewallStatus Read()
    {
        var type = Type.GetTypeFromProgID(PolicyProgId)
            ?? throw new InvalidOperationException("The Windows Firewall policy is not available.");
        dynamic policy = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("The Windows Firewall policy could not be opened.");
        try
        {
            var states = new Dictionary<WindowsFirewallProfile, bool>();
            foreach (var profile in Profiles)
            {
                states[profile] = (bool)policy.FirewallEnabled[(int)profile];
            }

            var active = (WindowsFirewallProfile)(int)policy.CurrentProfileTypes;
            return new WindowsFirewallStatus(states, active);
        }
        finally
        {
            if (Marshal.IsComObject(policy))
            {
                Marshal.ReleaseComObject(policy);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Could not read the Windows Firewall state; the firewall check falls back to Security Center.")]
    private partial void LogFailed(Exception ex);
}
