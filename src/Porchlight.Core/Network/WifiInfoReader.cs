using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="IWifiInfoReader"/>
/// <remarks>
/// Uses the Native Wifi API (<c>wlanapi.dll</c>) instead of parsing <c>netsh wlan show
/// interfaces</c>, whose labels are translated per Windows language. The returned buffers are read
/// at fixed offsets with <see cref="Marshal"/> rather than declared as structs: the offsets below
/// follow <c>WLAN_INTERFACE_INFO_LIST</c> and <c>WLAN_CONNECTION_ATTRIBUTES</c> in <c>wlanapi.h</c>.
/// </remarks>
public sealed partial class WifiInfoReader : IWifiInfoReader
{
    private const uint ClientVersion = 2;
    private const int OpcodeCurrentConnection = 7;
    private const int InterfaceStateConnected = 1;

    // WLAN_INTERFACE_INFO_LIST: DWORD count, DWORD index, then WLAN_INTERFACE_INFO[]:
    //   GUID (16) + WCHAR description[256] (512) + WLAN_INTERFACE_STATE (4) = 532 bytes.
    private const int ListHeaderSize = 8;
    private const int InterfaceInfoSize = 532;
    private const int GuidSize = 16;
    private const int InterfaceStateOffset = GuidSize + 512;

    // WLAN_CONNECTION_ATTRIBUTES: state (4) + mode (4) + WCHAR profile[256] (512), then
    // WLAN_ASSOCIATION_ATTRIBUTES starting at offset 520: DOT11_SSID (length + 32 bytes),
    // BSS type (4), BSSID (6 + 2 padding), PHY type (4), PHY index (4), signal quality (4).
    private const int AssociationOffset = 520;
    private const int SsidLengthOffset = AssociationOffset;
    private const int SsidBytesOffset = AssociationOffset + 4;
    private const int MaxSsidBytes = 32;
    private const int SignalQualityOffset = AssociationOffset + 56;
    private const int MinConnectionAttributesSize = SignalQualityOffset + 4;

    private readonly ILogger<WifiInfoReader> _logger;

    public WifiInfoReader(ILogger<WifiInfoReader> logger)
    {
        _logger = logger;
    }

    public WifiInfo? Read()
    {
        var client = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out client) != 0)
            {
                return null;
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out var list) != 0)
            {
                return null;
            }

            try
            {
                return ReadConnectedInterface(client, list);
            }
            finally
            {
                WlanFreeMemory(list);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
                                       or MarshalDirectiveException or ArgumentException)
        {
            // No Wi-Fi service / API on this machine (or a marshalling surprise): the page shows
            // "not available" for the Wi-Fi details, which is the honest answer.
            _logger.LogDebug(ex, "Could not read Wi-Fi details.");
            return null;
        }
        finally
        {
            if (client != IntPtr.Zero)
            {
                _ = WlanCloseHandle(client, IntPtr.Zero);
            }
        }
    }

    private static WifiInfo? ReadConnectedInterface(IntPtr client, IntPtr list)
    {
        var count = Marshal.ReadInt32(list, 0);
        for (var i = 0; i < count; i++)
        {
            var offset = ListHeaderSize + (i * InterfaceInfoSize);
            if (Marshal.ReadInt32(list, offset + InterfaceStateOffset) != InterfaceStateConnected)
            {
                continue;
            }

            var guidBytes = new byte[GuidSize];
            Marshal.Copy(list + offset, guidBytes, 0, GuidSize);
            var interfaceGuid = new Guid(guidBytes);

            // A non-zero result here includes "access denied", which Windows 11 24H2+ can return
            // when location permission is off - treated as "details not available".
            if (WlanQueryInterface(client, ref interfaceGuid, OpcodeCurrentConnection, IntPtr.Zero,
                    out var size, out var data, IntPtr.Zero) != 0)
            {
                continue;
            }

            try
            {
                return size < MinConnectionAttributesSize ? null : ParseConnection(data);
            }
            finally
            {
                WlanFreeMemory(data);
            }
        }

        return null;
    }

    private static WifiInfo ParseConnection(IntPtr data)
    {
        var length = Math.Clamp(Marshal.ReadInt32(data, SsidLengthOffset), 0, MaxSsidBytes);
        string? ssid = null;
        if (length > 0)
        {
            var bytes = new byte[length];
            Marshal.Copy(data + SsidBytesOffset, bytes, 0, length);
            ssid = Encoding.UTF8.GetString(bytes);
        }

        var signal = Math.Clamp(Marshal.ReadInt32(data, SignalQualityOffset), 0, 100);
        return new WifiInfo(string.IsNullOrWhiteSpace(ssid) ? null : ssid, signal);
    }

#pragma warning disable SYSLIB1054 // Fixed-offset buffer reads; the source generator adds nothing here.
    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(
        uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        int opCode,
        IntPtr pReserved,
        out uint pdwDataSize,
        out IntPtr ppData,
        IntPtr pWlanOpcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);
#pragma warning restore SYSLIB1054
}
