using Microsoft.Extensions.Logging;
using Porchlight.Core.Health;

namespace Porchlight.Core.Printing;

/// <summary>Reads <c>Win32_Printer</c> and counts <c>Win32_PrintJob</c> rows per printer.</summary>
public sealed partial class WmiPrinterSource : IPrinterSource
{
    private const string Scope = @"root\cimv2";

    private static readonly string[] PrinterProperties =
        ["Name", "Default", "WorkOffline", "PrinterStatus", "DetectedErrorState", "ExtendedPrinterStatus", "PortName", "Network"];

    private readonly ILogger<WmiPrinterSource> _logger;

    public WmiPrinterSource(ILogger<WmiPrinterSource> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<PrinterRawInfo> ReadAll()
    {
        try
        {
            var printers = WmiReader.Query(Scope, "SELECT * FROM Win32_Printer", PrinterProperties);
            var jobCounts = CountJobs();
            var list = new List<PrinterRawInfo>(printers.Count);
            foreach (var row in printers)
            {
                if (WmiReader.GetString(row, "Name") is not { Length: > 0 } name)
                {
                    continue;
                }

                list.Add(new PrinterRawInfo(
                    name,
                    WmiReader.GetBool(row, "Default") ?? false,
                    WmiReader.GetBool(row, "WorkOffline") ?? false,
                    WmiReader.GetInt(row, "PrinterStatus"),
                    WmiReader.GetInt(row, "DetectedErrorState"),
                    WmiReader.GetInt(row, "ExtendedPrinterStatus"),
                    WmiReader.GetString(row, "PortName"),
                    WmiReader.GetBool(row, "Network") ?? false,
                    jobCounts.GetValueOrDefault(name)));
            }

            return list;
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogReadFailed(ex);
            throw new InvalidOperationException("Could not read the printer list.", ex);
        }
    }

    private Dictionary<string, int> CountJobs()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var row in WmiReader.Query(Scope, "SELECT Name FROM Win32_PrintJob", ["Name"]))
            {
                if (PrinterClassifier.PrinterOfJob(WmiReader.GetString(row, "Name")) is { } printer)
                {
                    counts[printer] = counts.GetValueOrDefault(printer) + 1;
                }
            }
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            // The printers are still worth showing; the job count just reads as zero.
            LogJobsFailed(ex);
        }

        return counts;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the printer list.")]
    private partial void LogReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the print jobs; showing zero waiting.")]
    private partial void LogJobsFailed(Exception ex);
}
