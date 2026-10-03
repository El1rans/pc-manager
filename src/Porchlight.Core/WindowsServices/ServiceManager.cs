using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Porchlight.Core.WindowsServices;

/// <inheritdoc cref="IServiceManager"/>
public sealed partial class ServiceManager : IServiceManager
{
    private const int ErrorAccessDenied = 5;
    private const int ErrorServiceDoesNotExist = 1060;

    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceNoChange = 0xFFFFFFFF;
    private const uint ServiceAutoStart = 0x2;
    private const uint ServiceDemandStart = 0x3;
    private const uint ServiceDisabled = 0x4;
    private const uint ServiceConfigDelayedAutoStartInfo = 3;

    private readonly ILogger<ServiceManager> _logger;

    public ServiceManager(ILogger<ServiceManager> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetRunningDependents(string name)
    {
        try
        {
            using var controller = new ServiceController(name);
            return controller.DependentServices
                .Where(s => s.Status != ServiceControllerStatus.Stopped)
                .Select(s => s.DisplayName)
                .ToList();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            LogFailed(ex, "list the dependents of", name);
            return [];
        }
    }

    public ServiceChangeResult Start(string name, TimeSpan timeout) =>
        Run(name, "start", controller =>
        {
            controller.Refresh();
            if (controller.Status == ServiceControllerStatus.Running)
            {
                return ServiceChangeResult.Changed;
            }

            if (controller.Status != ServiceControllerStatus.StartPending)
            {
                controller.Start();
            }

            controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
            return ServiceChangeResult.Changed;
        });

    public ServiceChangeResult Stop(string name, TimeSpan timeout) =>
        Run(name, "stop", controller =>
        {
            controller.Refresh();
            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                return ServiceChangeResult.Changed;
            }

            if (controller.Status != ServiceControllerStatus.StopPending)
            {
                controller.Stop();
            }

            controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            return ServiceChangeResult.Changed;
        });

    public ServiceChangeResult SetStartType(string name, ServiceStartType startType)
    {
        var (startValue, delayed) = ToNative(startType);

        using var manager = ServiceNative.OpenSCManager(null, null, ScManagerConnect);
        if (manager.IsInvalid)
        {
            return Map(new Win32Exception(Marshal.GetLastWin32Error()), "change", name);
        }

        using var service = ServiceNative.OpenService(manager, name, ServiceChangeConfig);
        if (service.IsInvalid)
        {
            return Map(new Win32Exception(Marshal.GetLastWin32Error()), "change", name);
        }

        // Everything except the start type stays as it is (SERVICE_NO_CHANGE / null).
        if (!ServiceNative.ChangeServiceConfig(
                service, ServiceNoChange, startValue, ServiceNoChange, null, null, IntPtr.Zero, null, null, IntPtr.Zero, null))
        {
            return Map(new Win32Exception(Marshal.GetLastWin32Error()), "change", name);
        }

        if (startValue == ServiceAutoStart)
        {
            // Always written for automatic, so choosing plain "Starts with Windows" clears the delayed flag.
            var info = new ServiceNative.DelayedAutoStartInfo { DelayedAutostart = delayed };
            if (!ServiceNative.ChangeServiceConfig2(service, ServiceConfigDelayedAutoStartInfo, ref info))
            {
                return Map(new Win32Exception(Marshal.GetLastWin32Error()), "change", name);
            }
        }

        return ServiceChangeResult.Changed;
    }

    /// <summary>The SCM start value for <paramref name="startType"/> and whether the delayed
    /// auto-start flag must be set (only ever true for <see cref="ServiceStartType.AutomaticDelayed"/>).</summary>
    internal static (uint StartValue, bool Delayed) ToNative(ServiceStartType startType) =>
        startType switch
        {
            ServiceStartType.Automatic => (ServiceAutoStart, false),
            ServiceStartType.AutomaticDelayed => (ServiceAutoStart, true),
            ServiceStartType.Manual => (ServiceDemandStart, false),
            ServiceStartType.Disabled => (ServiceDisabled, false),
            _ => throw new ArgumentOutOfRangeException(nameof(startType), startType, "Unknown start type."),
        };

    private ServiceChangeResult Run(string name, string verb, Func<ServiceController, ServiceChangeResult> action)
    {
        try
        {
            using var controller = new ServiceController(name);
            return action(controller);
        }
        catch (System.ServiceProcess.TimeoutException ex)
        {
            LogFailed(ex, verb, name);
            return ServiceChangeResult.TimedOut;
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex.InnerException as Win32Exception ?? new Win32Exception(0, ex.Message), verb, name);
        }
        catch (Win32Exception ex)
        {
            return Map(ex, verb, name);
        }
    }

    private ServiceChangeResult Map(Win32Exception ex, string verb, string name)
    {
        switch (ex.NativeErrorCode)
        {
            case ErrorAccessDenied:
                return ServiceChangeResult.NeedsAdmin;
            case ErrorServiceDoesNotExist:
                return ServiceChangeResult.NotFound;
            default:
                LogFailed(ex, verb, name);
                return ServiceChangeResult.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not {Verb} the service {Name}.")]
    private partial void LogFailed(Exception ex, string verb, string name);

    // Plain DllImport (not LibraryImport) to avoid needing AllowUnsafeBlocks, like Tray/TrayNative.
    private static class ServiceNative
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct DelayedAutoStartInfo
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool DelayedAutostart;
        }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern ScHandle OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern ScHandle OpenService(ScHandle manager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChangeServiceConfig(
            ScHandle service, uint serviceType, uint startType, uint errorControl, string? binaryPathName,
            string? loadOrderGroup, IntPtr tagId, string? dependencies, string? serviceStartName, IntPtr password, string? displayName);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChangeServiceConfig2(ScHandle service, uint infoLevel, ref DelayedAutoStartInfo info);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseServiceHandle(IntPtr handle);

        /// <summary>An SCM or service handle, closed with <c>CloseServiceHandle</c>.</summary>
        internal sealed class ScHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public ScHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle() => CloseServiceHandle(handle);
        }
    }
}
