namespace Porchlight.Core.Printing;

/// <inheritdoc cref="IPrinterService"/>
public sealed class PrinterService : IPrinterService
{
    private readonly IPrinterSource _source;

    public PrinterService(IPrinterSource source)
    {
        _source = source;
    }

    public Task<IReadOnlyList<PrinterEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<PrinterEntry>>(
            () => _source.ReadAll()
                .Select(PrinterClassifier.ToEntry)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            cancellationToken);
}
