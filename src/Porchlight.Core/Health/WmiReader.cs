using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;

namespace Porchlight.Core.Health;

/// <summary>Small WMI helper shared by the health services: runs a query with a timeout and returns
/// plain property dictionaries so the callers (and their tests) never touch WMI types.</summary>
internal static class WmiReader
{
    /// <summary>Exceptions a WMI read can throw for ordinary reasons (missing class on this edition,
    /// access denied, service stopped, timeout). Callers turn these into "Couldn't check".</summary>
    public static bool IsExpected(Exception ex) =>
        ex is ManagementException or UnauthorizedAccessException or COMException
            or InvalidOperationException or TimeoutException;

    /// <summary>Runs <paramref name="query"/> in <paramref name="scopePath"/> (e.g. <c>root\wmi</c>)
    /// and returns one dictionary per instance holding the requested <paramref name="properties"/>
    /// (a missing property is simply absent). Blocking - call from a background thread.</summary>
    public static List<Dictionary<string, object?>> Query(
        string scopePath, string query, IReadOnlyList<string> properties)
    {
        var connection = new ConnectionOptions { Timeout = HealthTimeouts.WmiQuery };
        var scope = new ManagementScope(scopePath, connection);
        scope.Connect();

        var options = new System.Management.EnumerationOptions
        {
            Timeout = HealthTimeouts.WmiQuery,
            ReturnImmediately = false,
        };

        var rows = new List<Dictionary<string, object?>>();
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query), options);
        using var results = searcher.Get();
        foreach (var item in results)
        {
            using (item)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in properties)
                {
                    try
                    {
                        row[name] = item[name];
                    }
                    catch (ManagementException)
                    {
                        // Property not present on this Windows version: leave it out; callers treat
                        // an absent value as "not reported".
                    }
                }

                rows.Add(row);
            }
        }

        return rows;
    }

    public static long? GetLong(IReadOnlyDictionary<string, object?> row, string name) =>
        row.TryGetValue(name, out var value) && value is not null
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            : null;

    public static int? GetInt(IReadOnlyDictionary<string, object?> row, string name) =>
        GetLong(row, name) is { } l ? (int)Math.Clamp(l, int.MinValue, int.MaxValue) : null;

    public static bool? GetBool(IReadOnlyDictionary<string, object?> row, string name) =>
        row.TryGetValue(name, out var value) && value is bool b ? b : null;

    public static string? GetString(IReadOnlyDictionary<string, object?> row, string name) =>
        row.TryGetValue(name, out var value) ? value?.ToString() : null;

    public static IReadOnlyList<int> GetIntArray(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is not Array array)
        {
            return [];
        }

        var list = new List<int>(array.Length);
        foreach (var element in array)
        {
            if (element is not null)
            {
                list.Add(Convert.ToInt32(element, CultureInfo.InvariantCulture));
            }
        }

        return list;
    }
}
