using Microsoft.Win32;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="IStartupRegistry"/>
public sealed class StartupRegistry : IStartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyPath32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public IReadOnlyList<StartupRunValue> ReadRunValues(StartupSource source)
    {
        var (hive, path) = source switch
        {
            StartupSource.CurrentUserRun => (RegistryHive.CurrentUser, RunKeyPath),
            StartupSource.MachineRun => (RegistryHive.LocalMachine, RunKeyPath),
            StartupSource.MachineRun32 => (RegistryHive.LocalMachine, RunKeyPath32),
            _ => (default, null),
        };
        if (path is null)
        {
            return [];
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(path);
        if (key is null)
        {
            return [];
        }

        var values = new List<StartupRunValue>();
        foreach (var name in key.GetValueNames())
        {
            if (name.Length > 0 &&
                key.GetValueKind(name) is RegistryValueKind.String or RegistryValueKind.ExpandString &&
                key.GetValue(name, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) is string command)
            {
                values.Add(new StartupRunValue(name, command));
            }
        }

        return values;
    }

    public byte[]? ReadApproval(StartupSource source, string itemName)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemName);

        // A per-user approval overrides the per-machine one, so check it first for machine items.
        if (source.IsPerMachine() && ReadValue(RegistryHive.CurrentUser, source, itemName) is { } user)
        {
            return user;
        }

        return ReadValue(HiveFor(source), source, itemName);
    }

    public void WriteApproval(StartupSource source, string itemName, byte[] value)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemName);
        ArgumentNullException.ThrowIfNull(value);

        WriteValue(HiveFor(source), source, itemName, value);

        // If the user already has their own override for a machine item, keep it in step so the
        // effective state (per-user wins) matches what was just asked for.
        if (source.IsPerMachine() && ReadValue(RegistryHive.CurrentUser, source, itemName) is not null)
        {
            WriteValue(RegistryHive.CurrentUser, source, itemName, value);
        }
    }

    private static RegistryHive HiveFor(StartupSource source) =>
        source.IsPerMachine() ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

    private static string ApprovedSubKey(StartupSource source) => source switch
    {
        StartupSource.MachineRun32 => "Run32",
        StartupSource.CurrentUserFolder or StartupSource.MachineFolder => "StartupFolder",
        _ => "Run",
    };

    private static byte[]? ReadValue(RegistryHive hive, StartupSource source, string itemName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey($@"{ApprovedRoot}\{ApprovedSubKey(source)}");
        return key?.GetValue(itemName) as byte[];
    }

    private static void WriteValue(RegistryHive hive, StartupSource source, string itemName, byte[] value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey($@"{ApprovedRoot}\{ApprovedSubKey(source)}", writable: true);
        key.SetValue(itemName, value, RegistryValueKind.Binary);
    }
}
