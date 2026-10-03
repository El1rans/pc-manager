using Microsoft.Extensions.Logging;
using Porchlight.Core.Health;

namespace Porchlight.Core.Safety;

/// <inheritdoc cref="ISecurityCenterReader"/>
public sealed partial class SecurityCenterReader(ILogger<SecurityCenterReader> logger) : ISecurityCenterReader
{
    private const string Scope = @"root\SecurityCenter2";
    private const string NameProperty = "displayName";
    private const string StateProperty = "productState";

    public async Task<IReadOnlyList<SecurityProduct>?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(Read, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogUnavailable(ex);
            return null;
        }
    }

    private static List<SecurityProduct> Read()
    {
        var products = new List<SecurityProduct>();
        Add(products, "AntiVirusProduct", SecurityProductKind.Antivirus);
        Add(products, "FirewallProduct", SecurityProductKind.Firewall);
        return products;
    }

    private static void Add(List<SecurityProduct> into, string wmiClass, SecurityProductKind kind)
    {
        foreach (var row in WmiReader.Query(Scope, $"SELECT * FROM {wmiClass}", [NameProperty, StateProperty]))
        {
            var name = WmiReader.GetString(row, NameProperty);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var state = ProductStateParser.Parse(WmiReader.GetLong(row, StateProperty) ?? -1);
            into.Add(new SecurityProduct(name.Trim(), kind, state));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Windows Security Center is not available; showing 'couldn't check'.")]
    private partial void LogUnavailable(Exception ex);
}
