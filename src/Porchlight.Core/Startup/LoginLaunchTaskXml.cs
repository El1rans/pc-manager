using System.Xml;
using System.Xml.Linq;

namespace Porchlight.Core.Startup;

/// <summary>Builds and reads the Task Scheduler XML for the "start when I sign in" task.</summary>
public static class LoginLaunchTaskXml
{
    /// <summary>Name of the scheduled task (also referenced by the installer's uninstall step).</summary>
    public const string TaskName = "Porchlight";

    /// <summary>Command-line switch that starts Porchlight hidden in the tray.</summary>
    public const string TrayArgument = "--tray";

    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>Builds the task definition: runs <paramref name="exePath"/> <c>--tray</c> at
    /// <paramref name="userId"/>'s sign-in, elevated (HighestAvailable) with no UAC prompt. Priority 4
    /// is normal; Task Scheduler's default of 7 would run Porchlight at below-normal priority.</summary>
    public static string Build(string userId, string exePath)
    {
        var task = new XElement(
            Ns + "Task",
            new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description", "Starts Porchlight in the tray when you sign in to Windows.")),
            new XElement(
                Ns + "Triggers",
                new XElement(
                    Ns + "LogonTrigger",
                    new XElement(Ns + "Enabled", "true"),
                    new XElement(Ns + "UserId", userId),
                    new XElement(Ns + "Delay", "PT10S"))),
            new XElement(
                Ns + "Principals",
                new XElement(
                    Ns + "Principal",
                    new XAttribute("id", "Author"),
                    new XElement(Ns + "UserId", userId),
                    new XElement(Ns + "LogonType", "InteractiveToken"),
                    new XElement(Ns + "RunLevel", "HighestAvailable"))),
            new XElement(
                Ns + "Settings",
                new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(Ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(Ns + "StopIfGoingOnBatteries", "false"),
                new XElement(Ns + "AllowHardTerminate", "true"),
                new XElement(Ns + "StartWhenAvailable", "false"),
                new XElement(Ns + "AllowStartOnDemand", "true"),
                new XElement(Ns + "Enabled", "true"),
                new XElement(Ns + "Hidden", "false"),
                new XElement(Ns + "ExecutionTimeLimit", "PT0S"),
                new XElement(Ns + "Priority", "4")),
            new XElement(
                Ns + "Actions",
                new XAttribute("Context", "Author"),
                new XElement(
                    Ns + "Exec",
                    new XElement(Ns + "Command", exePath),
                    new XElement(Ns + "Arguments", TrayArgument),
                    new XElement(Ns + "WorkingDirectory", Path.GetDirectoryName(exePath) ?? string.Empty))));

        // XElement escapes every value (a path may contain '&' or '<'); the declaration is added by hand
        // because the file is written as UTF-16, which is what schtasks expects.
        return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" + Environment.NewLine + task;
    }

    /// <summary>Reads the command and arguments back out of task XML (as printed by
    /// <c>schtasks /Query /XML</c>); null if it is not a task with an Exec action.</summary>
    public static (string Command, string Arguments)? TryParseAction(string xml)
    {
        try
        {
            var exec = XDocument.Parse(xml).Descendants(Ns + "Exec").FirstOrDefault();
            var command = exec?.Element(Ns + "Command")?.Value.Trim().Trim('"');
            if (string.IsNullOrEmpty(command))
            {
                return null;
            }

            return (command, exec?.Element(Ns + "Arguments")?.Value.Trim() ?? string.Empty);
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
