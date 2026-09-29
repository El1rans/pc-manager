namespace Porchlight.Core.Startup;

/// <summary>
/// Parses and builds the binary value Windows keeps under
/// <c>...\Explorer\StartupApproved\{Run|Run32|StartupFolder}</c> - the same one Task Manager writes.
/// Layout: byte 0 is the state (even = enabled, odd = disabled; Windows itself writes
/// <c>0x02</c>/<c>0x06</c> and <c>0x03</c>), bytes 1-3 are zero, and bytes 4-11 are the FILETIME at
/// which the item was disabled (all zero when enabled).
/// </summary>
public static class StartupApprovedBlob
{
    /// <summary>Total length of a value Porchlight writes.</summary>
    public const int Length = 12;

    private const byte EnabledMarker = 0x02;
    private const byte DisabledMarker = 0x03;
    private const int FileTimeOffset = 4;

    /// <summary>A missing or empty value means Windows never recorded a choice, i.e. enabled.</summary>
    public static bool IsEnabled(byte[]? value) => value is not { Length: > 0 } || (value[0] & 1) == 0;

    /// <summary>The value for "enabled": <c>02</c> then eleven zero bytes.</summary>
    public static byte[] CreateEnabled()
    {
        var blob = new byte[Length];
        blob[0] = EnabledMarker;
        return blob;
    }

    /// <summary>The value for "disabled": <c>03</c>, three zero bytes and the FILETIME of
    /// <paramref name="disabledAt"/>.</summary>
    public static byte[] CreateDisabled(DateTimeOffset disabledAt)
    {
        var blob = new byte[Length];
        blob[0] = DisabledMarker;
        BitConverter.TryWriteBytes(blob.AsSpan(FileTimeOffset), disabledAt.ToFileTime());
        return blob;
    }

    /// <summary>The time a disabled value was written, or null when the value is enabled or too
    /// short to carry one.</summary>
    public static DateTimeOffset? GetDisabledAt(byte[]? value)
    {
        if (value is null || value.Length < Length || IsEnabled(value))
        {
            return null;
        }

        var fileTime = BitConverter.ToInt64(value, FileTimeOffset);
        return fileTime > 0 ? DateTimeOffset.FromFileTime(fileTime) : null;
    }
}
