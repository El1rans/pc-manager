using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.WindowsServices;

/// <summary>Reads <c>Win32_Service</c>, which returns resolved descriptions and the start mode.</summary>
public sealed partial class WmiServiceInfoSource : IServiceInfoSource
{
    private const string Query =
        "SELECT Name, DisplayName, Description, PathName, StartMode, DelayedAutoStart, State, ServiceType FROM Win32_Service";

    private static readonly TimeSpan WmiQueryTimeout = TimeSpan.FromSeconds(20);

    private readonly ILogger<WmiServiceInfoSource> _logger;

    public WmiServiceInfoSource(ILogger<WmiServiceInfoSource> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ServiceRawInfo> ReadAll()
    {
        var list = new List<ServiceRawInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(Query)
            {
                Options = new System.Management.EnumerationOptions { Timeout = WmiQueryTimeout },
            };
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    if (item["Name"] is not string name)
                    {
                        continue;
                    }

                    list.Add(new ServiceRawInfo(
                        name,
                        item["DisplayName"] as string ?? name,
                        item["Description"] as string,
                        item["PathName"] as string,
                        item["StartMode"] as string,
                        item["DelayedAutoStart"] is true,
                        item["State"] as string,
                        item["ServiceType"] as string));
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        {
            LogListFailed(ex);
        }

        return list;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not list Windows services.")]
    private partial void LogListFailed(Exception ex);
}
