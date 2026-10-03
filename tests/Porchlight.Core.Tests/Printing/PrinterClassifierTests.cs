using Porchlight.Core.Printing;
using Xunit;

namespace Porchlight.Core.Tests.Printing;

public sealed class PrinterClassifierTests
{
    [Theory]
    [InlineData("Microsoft Print to PDF", "PORTPROMPT:", true)]
    [InlineData("Microsoft XPS Document Writer", "PORTPROMPT:", true)]
    [InlineData("OneNote (Desktop)", "nul:", true)]
    [InlineData("OneNote for Windows 10", "Microsoft.Office.OneNote_16001.14326.20404.0_x64__8wekyb3d8bbwe_microsoft.onenoteim_S-1-5-21", true)]
    [InlineData("Fax", "SHRFAX:", true)]
    [InlineData("Adobe PDF", "Documents\\*.pdf", true)]
    [InlineData("Some Virtual Thing", "FILE:", true)]
    [InlineData("Brother HL-L2350DW", "IP_192.168.1.40", false)]
    [InlineData("HP DeskJet 2700", "USB001", false)]
    [InlineData("Canon MG3600 series", null, false)]
    public void IsVirtual_ClassifiesByNameAndPort(string name, string? port, bool expected) =>
        Assert.Equal(expected, PrinterClassifier.IsVirtual(name, port));

    [Theory]
    [InlineData("HP LaserJet, 12", "HP LaserJet")]
    [InlineData("Office, Floor 2 Printer, 7", "Office, Floor 2 Printer")]
    [InlineData("NoSeparator", null)]
    [InlineData(null, null)]
    public void PrinterOfJob_TakesEverythingBeforeTheLastSeparator(string? jobName, string? expected) =>
        Assert.Equal(expected, PrinterClassifier.PrinterOfJob(jobName));

    [Fact]
    public void ToEntry_DecodesStatusAndClassifies()
    {
        var raw = new PrinterRawInfo("HP DeskJet 2700", false, true, 3, 2, 3, "USB001", false, 2);

        var entry = PrinterClassifier.ToEntry(raw);

        Assert.Equal(PrinterStatusKind.Offline, entry.Status);
        Assert.False(entry.IsVirtual);
        Assert.Equal(2, entry.JobCount);
        Assert.True(entry.WorkOffline);
    }

    [Theory]
    [InlineData("00001.SPL", true)]
    [InlineData("00001.shd", true)]
    [InlineData(@"C:\Windows\System32\spool\PRINTERS\00002.SPL", true)]
    [InlineData("readme.txt", false)]
    [InlineData("FP00001.TMP", false)]
    [InlineData("SPL", false)]
    public void SpoolFileFilter_OnlyMatchesSplAndShd(string fileName, bool expected) =>
        Assert.Equal(expected, SpoolFileFilter.IsSpoolFile(fileName));
}
