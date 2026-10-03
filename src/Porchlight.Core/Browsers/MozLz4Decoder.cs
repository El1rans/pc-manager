using System.Buffers.Binary;

namespace Porchlight.Core.Browsers;

/// <summary>
/// Decodes Firefox's <c>.mozlz4</c> files (an 8-byte magic, a 4-byte little-endian size, then one
/// LZ4 block). Written for untrusted input: every read and copy is bounds-checked and the output size is
/// capped, so a damaged file throws <see cref="InvalidDataException"/> instead of misbehaving.
/// </summary>
internal static class MozLz4Decoder
{
    /// <summary>Largest decompressed size accepted (a search config is a few hundred KB at most).</summary>
    public const int MaxOutputBytes = 8 * 1024 * 1024;

    private const int HeaderSize = 12;
    private const int MinMatch = 4;
    private const int LengthNibble = 15;
    private const byte LengthContinues = 255;
    private static readonly byte[] Magic = "mozLz40\0"u8.ToArray();

    public static byte[] Decode(ReadOnlySpan<byte> file)
    {
        if (file.Length < HeaderSize || !file[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a mozlz4 file.");
        }

        var size = BinaryPrimitives.ReadUInt32LittleEndian(file[Magic.Length..]);
        if (size > MaxOutputBytes)
        {
            throw new InvalidDataException("Decompressed size is too large.");
        }

        var input = file[HeaderSize..];
        var output = new byte[size];
        var ip = 0;
        var op = 0;
        while (ip < input.Length)
        {
            var token = input[ip++];
            var literals = ReadLength(input, ref ip, token >> 4);
            if (literals > input.Length - ip || literals > output.Length - op)
            {
                throw new InvalidDataException("Literal run is out of range.");
            }

            input.Slice(ip, literals).CopyTo(output.AsSpan(op));
            ip += literals;
            op += literals;
            if (ip >= input.Length)
            {
                break; // The last sequence holds literals only.
            }

            if (input.Length - ip < 2)
            {
                throw new InvalidDataException("Truncated match offset.");
            }

            var offset = input[ip] | (input[ip + 1] << 8);
            ip += 2;
            if (offset == 0 || offset > op)
            {
                throw new InvalidDataException("Match offset is out of range.");
            }

            var matchLength = ReadLength(input, ref ip, token & LengthNibble) + MinMatch;
            if (matchLength > output.Length - op)
            {
                throw new InvalidDataException("Match runs past the end of the output.");
            }

            for (var i = 0; i < matchLength; i++)
            {
                output[op] = output[op - offset];
                op++;
            }
        }

        if (op != output.Length)
        {
            throw new InvalidDataException("Decompressed size does not match the header.");
        }

        return output;
    }

    private static int ReadLength(ReadOnlySpan<byte> input, ref int ip, int nibble)
    {
        long length = nibble;
        if (nibble == LengthNibble)
        {
            byte next;
            do
            {
                if (ip >= input.Length)
                {
                    throw new InvalidDataException("Truncated length.");
                }

                next = input[ip++];
                length += next;
                if (length > MaxOutputBytes)
                {
                    throw new InvalidDataException("Length is too large.");
                }
            }
            while (next == LengthContinues);
        }

        return (int)length;
    }
}
