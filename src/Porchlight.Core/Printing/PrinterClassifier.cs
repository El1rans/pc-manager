namespace Porchlight.Core.Printing;

/// <summary>Pure helpers: virtual-printer detection and print-job name parsing.</summary>
public static class PrinterClassifier
{
    private const string JobNameSeparator = ", ";

    private static readonly string[] VirtualNameParts =
    [
        "Microsoft Print to PDF",
        "Microsoft XPS Document Writer",
        "OneNote",
        "Fax",
        "Send To",
        "Snagit",
        "PDF",
    ];

    private static readonly string[] VirtualPortPrefixes = ["PORTPROMPT:", "NUL:", "FILE:", "XPSPORT:", "SHRFAX:"];

    /// <summary>True for "printers" that make a file or send a fax instead of printing on paper.</summary>
    public static bool IsVirtual(string name, string? portName)
    {
        foreach (var part in VirtualNameParts)
        {
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return portName is { Length: > 0 }
            && VirtualPortPrefixes.Any(p => portName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Win32_PrintJob names look like "HP LaserJet, 12"; returns the printer part (the job id
    /// follows the last separator, the printer name itself may contain commas), or null.</summary>
    public static string? PrinterOfJob(string? jobName)
    {
        if (jobName is null)
        {
            return null;
        }

        var index = jobName.LastIndexOf(JobNameSeparator, StringComparison.Ordinal);
        return index > 0 ? jobName[..index] : null;
    }

    /// <summary>Builds the displayable entry: classified and decoded.</summary>
    public static PrinterEntry ToEntry(PrinterRawInfo raw) => new(
        raw.Name,
        raw.IsDefault,
        raw.WorkOffline,
        PrinterStatusDecoder.Decode(raw.PrinterStatus, raw.DetectedErrorState, raw.ExtendedPrinterStatus, raw.WorkOffline),
        raw.PortName,
        raw.IsNetwork,
        raw.JobCount,
        IsVirtual(raw.Name, raw.PortName));
}
