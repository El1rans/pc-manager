using System.Buffers.Binary;
using System.Text;
using Porchlight.Core.Browsers;
using Xunit;

namespace Porchlight.Core.Tests.Browsers;

public sealed class MozLz4DecoderTests
{
    /// <summary>Wraps <paramref name="block"/> in the mozlz4 header.</summary>
    internal static byte[] Wrap(byte[] block, int decompressedSize)
    {
        var file = new byte[12 + block.Length];
        Encoding.ASCII.GetBytes("mozLz40\0").CopyTo(file, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)decompressedSize);
        block.CopyTo(file, 12);
        return file;
    }

    /// <summary>A valid LZ4 block holding only literals (no compression), enough for fixtures.</summary>
    internal static byte[] LiteralsOnly(byte[] data)
    {
        var block = new List<byte>();
        if (data.Length < 15)
        {
            block.Add((byte)(data.Length << 4));
        }
        else
        {
            block.Add(0xF0);
            var rest = data.Length - 15;
            while (rest >= 255)
            {
                block.Add(255);
                rest -= 255;
            }

            block.Add((byte)rest);
        }

        block.AddRange(data);
        return [.. block];
    }

    internal static byte[] MozLz4(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        return Wrap(LiteralsOnly(data), data.Length);
    }

    [Fact]
    public void LiteralsOnly_RoundTrips_IncludingLongRuns()
    {
        var text = new string('a', 600) + "end";

        var decoded = MozLz4Decoder.Decode(MozLz4(text));

        Assert.Equal(text, Encoding.UTF8.GetString(decoded));
    }

    [Fact]
    public void MatchSequences_AreExpanded()
    {
        // "abcd", then copy 8 bytes from 4 back (overlapping), then the literal "e".
        byte[] block = [0x44, (byte)'a', (byte)'b', (byte)'c', (byte)'d', 0x04, 0x00, 0x10, (byte)'e'];

        var decoded = MozLz4Decoder.Decode(Wrap(block, 13));

        Assert.Equal("abcdabcdabcde", Encoding.ASCII.GetString(decoded));
    }

    [Fact]
    public void BadMagic_Throws()
    {
        var file = MozLz4("hello");
        file[0] = (byte)'x';

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(file));
    }

    [Fact]
    public void TooShort_Throws() =>
        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(new byte[5]));

    [Fact]
    public void HugeDeclaredSize_Throws()
    {
        var file = Wrap(LiteralsOnly([1]), int.MaxValue);

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(file));
    }

    [Fact]
    public void SizeMismatch_Throws()
    {
        var file = Wrap(LiteralsOnly("hello"u8.ToArray()), 99);

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(file));
    }

    [Fact]
    public void LiteralRunPastTheInput_Throws()
    {
        byte[] block = [0x50, (byte)'a']; // claims 5 literals, has 1

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(Wrap(block, 5)));
    }

    [Fact]
    public void MatchOffsetBeforeTheStart_Throws()
    {
        byte[] block = [0x10, (byte)'a', 0x09, 0x00]; // offset 9 with only 1 byte written

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(Wrap(block, 5)));
    }

    [Fact]
    public void ZeroOffset_Throws()
    {
        byte[] block = [0x10, (byte)'a', 0x00, 0x00];

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(Wrap(block, 5)));
    }

    [Fact]
    public void MatchPastTheOutput_Throws()
    {
        byte[] block = [0x1F, (byte)'a', 0x01, 0x00, 0xFF, 0xFF]; // asks for a huge match

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(Wrap(block, 10)));
    }

    [Fact]
    public void TruncatedOffset_Throws()
    {
        byte[] block = [0x10, (byte)'a', 0x01];

        Assert.Throws<InvalidDataException>(() => MozLz4Decoder.Decode(Wrap(block, 5)));
    }
}
