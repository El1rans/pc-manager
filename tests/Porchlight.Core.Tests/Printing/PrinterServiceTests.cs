using Porchlight.Core.Printing;
using Xunit;

namespace Porchlight.Core.Tests.Printing;

public sealed class PrinterServiceTests
{
    private static PrinterRawInfo Raw(string name, bool isDefault = false, string? port = "USB001") =>
        new(name, isDefault, false, 3, 2, 3, port, false, 0);

    [Fact]
    public async Task List_PutsDefaultFirstThenSortsByNameAndFlagsVirtualOnes()
    {
        var source = new FakeSource(
            Raw("Zebra"), Raw("Microsoft Print to PDF", port: "PORTPROMPT:"), Raw("Brother", isDefault: false), Raw("Canon", isDefault: true));

        var list = await new PrinterService(source).ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Canon", "Brother", "Microsoft Print to PDF", "Zebra"], list.Select(p => p.Name));
        Assert.True(list[0].IsDefault);
        Assert.Equal(["Microsoft Print to PDF"], list.Where(p => p.IsVirtual).Select(p => p.Name));
    }

    [Fact]
    public async Task List_PropagatesReadFailure()
    {
        var service = new PrinterService(new FakeSource { Throw = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FakeSource : IPrinterSource
    {
        private readonly PrinterRawInfo[] _items;

        public FakeSource(params PrinterRawInfo[] items)
        {
            _items = items;
        }

        public bool Throw { get; init; }

        public IReadOnlyList<PrinterRawInfo> ReadAll() =>
            Throw ? throw new InvalidOperationException("boom") : _items;
    }
}
