namespace Porchlight.Core.Printing;

/// <summary>Reads the installed printers and their waiting jobs (read-only).</summary>
public interface IPrinterSource
{
    /// <summary>Blocking; call from a background thread. Throws <see cref="InvalidOperationException"/>
    /// when the printer list cannot be read.</summary>
    IReadOnlyList<PrinterRawInfo> ReadAll();
}
