using System.Runtime.InteropServices;

namespace Porchlight.Core.Cleanup;

/// <summary>Splits an uninstall registry <c>UninstallString</c> into a program and arguments with
/// Windows' own <c>CommandLineToArgvW</c> rules, handling the common forms
/// <c>"C:\x\unins000.exe" /arg</c>, <c>MsiExec.exe /X{GUID}</c> and the unquoted
/// <c>C:\Program Files\App\uninstall.exe /S</c>.</summary>
public static class UninstallCommandParser
{
    private const string ExeExtension = ".exe";

    /// <summary>Returns null if <paramref name="commandLine"/> is empty or cannot be parsed.</summary>
    public static UninstallCommand? Parse(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var text = commandLine.Trim();
        if (text[0] != '"' && FindExecutableEnd(text) is { } exeEnd)
        {
            // Unquoted: the program is everything up to the first ".exe" that ends a word, which
            // may itself contain spaces (C:\Program Files\...). CommandLineToArgvW would split there.
            var arguments = SplitArguments(text[exeEnd..]);
            return arguments is null ? null : new UninstallCommand(text[..exeEnd], arguments);
        }

        var all = SplitArguments(text, includeFirst: true);
        return all is { Count: > 0 } ? new UninstallCommand(all[0], all.Skip(1).ToList()) : null;
    }

    /// <summary>Index just past the first <c>.exe</c> that is followed by whitespace or the end.</summary>
    private static int? FindExecutableEnd(string text)
    {
        var searchFrom = 0;
        while (searchFrom < text.Length)
        {
            var index = text.IndexOf(ExeExtension, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var end = index + ExeExtension.Length;
            if (end == text.Length || char.IsWhiteSpace(text[end]))
            {
                return end;
            }

            searchFrom = end;
        }

        return null;
    }

    private static List<string>? SplitArguments(string text, bool includeFirst = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // CommandLineToArgvW treats its first token specially (it is meant to be the program), so
        // arguments alone are parsed behind a placeholder program name that is then dropped.
        var argv = CommandLineToArgv(includeFirst ? text : "x " + text);
        if (argv is null)
        {
            return null;
        }

        return includeFirst ? argv : argv.Skip(1).ToList();
    }

    private static List<string>? CommandLineToArgv(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var count);
        if (argv == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var result = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var pointer = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                result.Add(Marshal.PtrToStringUni(pointer) ?? string.Empty);
            }

            return result;
        }
        finally
        {
            _ = LocalFree(argv);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
