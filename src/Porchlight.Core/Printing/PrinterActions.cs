using System.Management;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Health;
using Porchlight.Core.WindowsServices;

namespace Porchlight.Core.Printing;

/// <summary>The real <see cref="IPrinterActions"/>: WMI for printers and jobs, the shared
/// <see cref="IServiceManager"/> for the print service. Only tests with fakes exercise callers; this
/// class is verified on a real PC.</summary>
public sealed partial class PrinterActions : IPrinterActions
{
    public const string SpoolerServiceName = "Spooler";

    /// <summary>How long to wait for the print service to stop or start.</summary>
    public static readonly TimeSpan SpoolerTimeout = TimeSpan.FromSeconds(30);

    private const string Scope = @"root\cimv2";
    private const uint WmiSuccess = 0;
    private const uint WmiAccessDenied = 5;

    private readonly IServiceManager _services;
    private readonly ILogger<PrinterActions> _logger;

    public PrinterActions(IServiceManager services, ILogger<PrinterActions> logger)
    {
        _services = services;
        _logger = logger;
    }

    private static string SpoolFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "PRINTERS");

    public Task<PrinterActionOutcome> SetDefaultAsync(string printerName, CancellationToken cancellationToken) =>
        Task.Run(() => InvokeOnPrinter(printerName, "SetDefaultPrinter"), cancellationToken);

    public Task<PrinterActionOutcome> PrintTestPageAsync(string printerName, CancellationToken cancellationToken) =>
        Task.Run(() => InvokeOnPrinter(printerName, "PrintTestPage"), cancellationToken);

    public Task<PrinterActionOutcome> UseOnlineAsync(string printerName, CancellationToken cancellationToken) =>
        Task.Run(() => SetOnline(printerName), cancellationToken);

    public Task<PrinterActionOutcome> ClearJobsAsync(string printerName, CancellationToken cancellationToken) =>
        Task.Run(() => ClearJobs(printerName, cancellationToken), cancellationToken);

    public Task<SpoolerState> GetSpoolerStateAsync(CancellationToken cancellationToken) =>
        Task.Run(ReadSpoolerState, cancellationToken);

    public Task<PrinterActionOutcome> StartSpoolerAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Map(_services.Start(SpoolerServiceName, SpoolerTimeout)), cancellationToken);

    public Task<PrinterActionOutcome> RestartSpoolerAsync(bool clearSpoolFiles, CancellationToken cancellationToken) =>
        Task.Run(() => RestartSpooler(clearSpoolFiles), cancellationToken);

    private static PrinterActionOutcome Map(ServiceChangeResult result) => PrinterActionOutcome.Of(result switch
    {
        ServiceChangeResult.Changed => PrinterActionResult.Done,
        ServiceChangeResult.NeedsAdmin => PrinterActionResult.NeedsAdmin,
        ServiceChangeResult.NotFound => PrinterActionResult.NotFound,
        ServiceChangeResult.TimedOut => PrinterActionResult.TimedOut,
        _ => PrinterActionResult.Failed,
    });

    private static string Escape(string value) => value.Replace(@"\", @"\\").Replace("'", @"\'");

    private static PrinterActionOutcome FromReturnCode(uint code) => PrinterActionOutcome.Of(code switch
    {
        WmiSuccess => PrinterActionResult.Done,
        WmiAccessDenied => PrinterActionResult.NeedsAdmin,
        _ => PrinterActionResult.Failed,
    });

    private PrinterActionOutcome InvokeOnPrinter(string printerName, string method)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                Scope, $"SELECT * FROM Win32_Printer WHERE Name = '{Escape(printerName)}'");
            foreach (ManagementObject printer in searcher.Get())
            {
                using (printer)
                {
                    return FromReturnCode(Convert.ToUInt32(printer.InvokeMethod(method, null), System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            return PrinterActionOutcome.Of(PrinterActionResult.NotFound);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            return FromException(ex, method);
        }
    }

    private PrinterActionOutcome SetOnline(string printerName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                Scope, $"SELECT * FROM Win32_Printer WHERE Name = '{Escape(printerName)}'");
            foreach (ManagementObject printer in searcher.Get())
            {
                using (printer)
                {
                    printer["WorkOffline"] = false;
                    printer.Put();
                    return PrinterActionOutcome.Of(PrinterActionResult.Done);
                }
            }

            return PrinterActionOutcome.Of(PrinterActionResult.NotFound);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            return FromException(ex, "use online");
        }
    }

    private PrinterActionOutcome ClearJobs(string printerName, CancellationToken cancellationToken)
    {
        var cleared = 0;
        var failed = false;
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM Win32_PrintJob");
            foreach (ManagementObject job in searcher.Get())
            {
                using (job)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var owner = PrinterClassifier.PrinterOfJob(job["Name"] as string);
                    if (!string.Equals(owner, printerName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (Convert.ToUInt32(job.InvokeMethod("Delete", null), System.Globalization.CultureInfo.InvariantCulture) == WmiSuccess)
                    {
                        cleared++;
                    }
                    else
                    {
                        failed = true;
                    }
                }
            }
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            var outcome = FromException(ex, "clear jobs");
            return outcome with { Count = cleared };
        }

        return new PrinterActionOutcome(failed ? PrinterActionResult.Failed : PrinterActionResult.Done, cleared);
    }

    private SpoolerState ReadSpoolerState()
    {
        try
        {
            using var controller = new ServiceController(SpoolerServiceName);
            return controller.Status switch
            {
                ServiceControllerStatus.Running => SpoolerState.Running,
                ServiceControllerStatus.Stopped => SpoolerState.Stopped,
                ServiceControllerStatus.Paused => SpoolerState.Unknown,
                _ => SpoolerState.Changing,
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LogFailed(ex, "read the state of the print service");
            return SpoolerState.Unknown;
        }
    }

    private PrinterActionOutcome RestartSpooler(bool clearSpoolFiles)
    {
        var stopped = Map(_services.Stop(SpoolerServiceName, SpoolerTimeout));
        if (!stopped.IsDone)
        {
            return stopped;
        }

        try
        {
            if (clearSpoolFiles)
            {
                DeleteSpoolFiles();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never leave the PC without a print service: still start it below.
            LogFailed(ex, "read the print job folder");
        }

        return Map(_services.Start(SpoolerServiceName, SpoolerTimeout));
    }

    private void DeleteSpoolFiles()
    {
        var folder = SpoolFolder;
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder).Where(f => SpoolFileFilter.IsSpoolFile(f)))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // One locked file must not stop the restart; the next print will reuse the queue.
                LogFailed(ex, "delete a leftover print job file");
            }
        }
    }

    private PrinterActionOutcome FromException(Exception ex, string what)
    {
        if (ex is UnauthorizedAccessException
            || ex is ManagementException { ErrorCode: ManagementStatus.AccessDenied })
        {
            return PrinterActionOutcome.Of(PrinterActionResult.NeedsAdmin);
        }

        if (ex is ManagementException { ErrorCode: ManagementStatus.NotSupported or ManagementStatus.ProviderNotCapable or ManagementStatus.ReadOnly })
        {
            return PrinterActionOutcome.Of(PrinterActionResult.NotSupported);
        }

        LogFailed(ex, what);
        return PrinterActionOutcome.Of(PrinterActionResult.Failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Printer action failed: {What}.")]
    private partial void LogFailed(Exception ex, string what);
}
