using System.Globalization;
using Porchlight.Core.Network;

namespace Porchlight.App.Features.Network;

/// <summary>One row of the "Apps using the internet" list.</summary>
public sealed class NetworkAppRow
{
    public NetworkAppRow(NetworkAppUsage usage)
    {
        Name = usage.Name;
        CountText = string.Create(
            CultureInfo.CurrentCulture,
            $"{usage.ConnectionCount} {(usage.ConnectionCount == 1 ? "connection" : "connections")}");
    }

    public string Name { get; }

    public string CountText { get; }
}
