using System.Text;

namespace Porchlight.Core.WebConsole;

/// <summary>Reads an HTTP request's head (everything up to the blank line after the headers) off a
/// stream, never buffering more than <see cref="WebConsoleOptions.MaxRequestHeadBytes"/>.</summary>
internal static class HttpRequestHeadReader
{
    private static readonly byte[] HeadTerminator = "\r\n\r\n"u8.ToArray();

    /// <returns><c>TooLarge</c> when the head does not fit in the limit; otherwise <c>Head</c> is the
    /// head without its terminating blank line, or null when the client closed the connection before
    /// sending a complete one.</returns>
    public static async Task<(bool TooLarge, string? Head)> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[WebConsoleOptions.MaxRequestHeadBytes];
        var length = 0;

        while (true)
        {
            if (length == buffer.Length)
            {
                return (true, null);
            }

            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return (false, null);
            }

            // The terminator may straddle two reads, so re-scan the last few bytes already held.
            var searchStart = Math.Max(0, length - (HeadTerminator.Length - 1));
            length += read;
            var end = buffer.AsSpan(searchStart, length - searchStart).IndexOf(HeadTerminator);
            if (end >= 0)
            {
                return (false, Encoding.Latin1.GetString(buffer, 0, searchStart + end));
            }
        }
    }
}
