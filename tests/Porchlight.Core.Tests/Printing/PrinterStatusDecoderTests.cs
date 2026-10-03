using Porchlight.Core.Printing;
using Xunit;

namespace Porchlight.Core.Tests.Printing;

public sealed class PrinterStatusDecoderTests
{
    [Theory]
    // printerStatus, detectedErrorState, extendedPrinterStatus, workOffline, expected
    [InlineData(3, 2, 3, false, PrinterStatusKind.Ready)]
    [InlineData(null, null, null, false, PrinterStatusKind.Ready)]
    [InlineData(3, 3, 3, false, PrinterStatusKind.Ready)] // low paper is still ready
    [InlineData(3, 5, 3, false, PrinterStatusKind.Ready)] // low toner is still ready
    [InlineData(4, 2, 4, false, PrinterStatusKind.Printing)]
    [InlineData(3, 2, 17, false, PrinterStatusKind.Printing)] // I/O active
    [InlineData(7, 2, 7, false, PrinterStatusKind.Offline)]
    [InlineData(3, 9, 3, false, PrinterStatusKind.Offline)]
    [InlineData(3, 2, 11, false, PrinterStatusKind.Offline)] // not available
    [InlineData(3, 2, 3, true, PrinterStatusKind.Offline)] // "use printer offline"
    [InlineData(3, 4, 3, false, PrinterStatusKind.OutOfPaper)]
    [InlineData(3, 8, 3, false, PrinterStatusKind.PaperJam)]
    [InlineData(3, 2, 8, false, PrinterStatusKind.Paused)]
    [InlineData(3, 2, 9, false, PrinterStatusKind.Error)]
    [InlineData(6, 2, 6, false, PrinterStatusKind.Error)] // stopped printing
    [InlineData(3, 7, 3, false, PrinterStatusKind.Error)] // door open
    [InlineData(3, 10, 3, false, PrinterStatusKind.Error)] // service requested
    [InlineData(3, 11, 3, false, PrinterStatusKind.Error)] // output bin full
    public void Decode_MapsWmiCodes(int? status, int? error, int? extended, bool workOffline, PrinterStatusKind expected) =>
        Assert.Equal(expected, PrinterStatusDecoder.Decode(status, error, extended, workOffline));

    [Fact]
    public void Decode_HardwareProblemBeatsOffline()
    {
        Assert.Equal(PrinterStatusKind.PaperJam, PrinterStatusDecoder.Decode(7, 8, 7, true));
        Assert.Equal(PrinterStatusKind.OutOfPaper, PrinterStatusDecoder.Decode(3, 4, 7, false));
    }

    [Fact]
    public void Decode_OfflineBeatsPausedAndError() =>
        Assert.Equal(PrinterStatusKind.Offline, PrinterStatusDecoder.Decode(3, 2, 8, true));

    [Theory]
    [InlineData(PrinterStatusKind.Ready, "Ready")]
    [InlineData(PrinterStatusKind.Printing, "Printing")]
    [InlineData(PrinterStatusKind.Offline, "Offline")]
    [InlineData(PrinterStatusKind.OutOfPaper, "Out of paper")]
    [InlineData(PrinterStatusKind.PaperJam, "Paper jam")]
    [InlineData(PrinterStatusKind.Paused, "Paused")]
    [InlineData(PrinterStatusKind.Error, "Error")]
    public void Label_IsPlainWords(PrinterStatusKind kind, string expected) =>
        Assert.Equal(expected, PrinterStatusDecoder.Label(kind));
}
