using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Elevation;
using Porchlight.Core.Processes;
using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class LoginLaunchTests
{
    private const string Exe = @"C:\Program Files\Porchlight\Porchlight.exe";
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Fact]
    public void Build_HasElevatedLogonTriggerAtNormalPriorityRunningTrayArgument()
    {
        var doc = XDocument.Parse(LoginLaunchTaskXml.Build(@"PC\Sam", Exe));

        Assert.Equal("4", doc.Descendants(Ns + "Priority").Single().Value);
        Assert.Equal("HighestAvailable", doc.Descendants(Ns + "RunLevel").Single().Value);
        Assert.Equal("InteractiveToken", doc.Descendants(Ns + "LogonType").Single().Value);
        Assert.Equal(@"PC\Sam", doc.Descendants(Ns + "LogonTrigger").Single().Element(Ns + "UserId")!.Value);
        Assert.Equal(@"PC\Sam", doc.Descendants(Ns + "Principal").Single().Element(Ns + "UserId")!.Value);
        Assert.Equal(Exe, doc.Descendants(Ns + "Command").Single().Value);
        Assert.Equal("--tray", doc.Descendants(Ns + "Arguments").Single().Value);
        Assert.Equal(@"C:\Program Files\Porchlight", doc.Descendants(Ns + "WorkingDirectory").Single().Value);
    }

    [Fact]
    public void Build_SetsBatteryTimeLimitAndInstanceSettings()
    {
        var settings = XDocument.Parse(LoginLaunchTaskXml.Build(@"PC\Sam", Exe)).Descendants(Ns + "Settings").Single();

        Assert.Equal("false", settings.Element(Ns + "DisallowStartIfOnBatteries")!.Value);
        Assert.Equal("false", settings.Element(Ns + "StopIfGoingOnBatteries")!.Value);
        Assert.Equal("PT0S", settings.Element(Ns + "ExecutionTimeLimit")!.Value);
        Assert.Equal("IgnoreNew", settings.Element(Ns + "MultipleInstancesPolicy")!.Value);
        Assert.Equal("true", settings.Element(Ns + "AllowHardTerminate")!.Value);
        Assert.Equal("false", settings.Element(Ns + "StartWhenAvailable")!.Value);
        Assert.Equal("true", settings.Element(Ns + "Enabled")!.Value);
    }

    [Fact]
    public void Build_EscapesXmlSpecialCharactersInPathAndUser()
    {
        var xml = LoginLaunchTaskXml.Build(@"DOM\A&B", @"C:\R&D <x>\Porchlight.exe");

        Assert.Contains("R&amp;D &lt;x&gt;", xml, StringComparison.Ordinal);
        var parsed = LoginLaunchTaskXml.TryParseAction(xml);
        Assert.Equal(@"C:\R&D <x>\Porchlight.exe", parsed?.Command);
        Assert.Equal(@"DOM\A&B", XDocument.Parse(xml).Descendants(Ns + "Principal").Single().Element(Ns + "UserId")!.Value);
    }

    [Fact]
    public void TryParseAction_ReadsCommandAndArguments_AndStripsQuotes()
    {
        var xml = LoginLaunchTaskXml.Build("u", Exe).Replace(Exe, "\"" + Exe + "\"", StringComparison.Ordinal);

        var parsed = LoginLaunchTaskXml.TryParseAction(xml);

        Assert.Equal((Exe, "--tray"), parsed);
    }

    [Theory]
    [InlineData("not xml")]
    [InlineData("<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"/>")]
    public void TryParseAction_ReturnsNullForUnusableXml(string xml) =>
        Assert.Null(LoginLaunchTaskXml.TryParseAction(xml));

    [Fact]
    public async Task Enable_Elevated_RunsSchtasksCreateDirectlyWithTheXmlFile()
    {
        var h = new Harness { IsElevated = true };

        var result = await h.Service.EnableAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        var call = Assert.Single(h.Runner.Calls);
        Assert.Equal("schtasks.exe", call.File);
        Assert.Equal(["/Create", "/TN", "Porchlight", "/XML", call.Args[4], "/F"], call.Args);
        Assert.Contains("HighestAvailable", h.Runner.XmlSeenOnCreate, StringComparison.Ordinal);
        Assert.Contains(Exe, h.Runner.XmlSeenOnCreate, StringComparison.Ordinal);
        Assert.Empty(h.Elevated.Calls);
        Assert.False(File.Exists(call.Args[4]), "temp XML should be cleaned up");
    }

    [Fact]
    public async Task Enable_NotElevated_UsesTheElevatedRunner()
    {
        var h = new Harness { IsElevated = false };

        var result = await h.Service.EnableAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(h.Runner.Calls);
        var call = Assert.Single(h.Elevated.Calls);
        Assert.Equal("schtasks.exe", call.File);
        Assert.Equal("/Create", call.Args[0]);
    }

    [Fact]
    public async Task Enable_UacDeclined_ReportsDeclined()
    {
        var h = new Harness { IsElevated = false };
        h.Elevated.Next = new ElevatedRunResult(Declined: true, ExitCode: -1);

        var result = await h.Service.EnableAsync(CancellationToken.None);

        Assert.Equal(LoginLaunchOutcome.Declined, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }

    [Fact]
    public async Task Enable_NonZeroExit_ReportsFailed()
    {
        var h = new Harness { IsElevated = true };
        h.Runner.CreateExitCode = 1;

        var result = await h.Service.EnableAsync(CancellationToken.None);

        Assert.Equal(LoginLaunchOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Enable_UnknownExePath_Fails()
    {
        var h = new Harness { IsElevated = true, ExePath = null };

        var result = await h.Service.EnableAsync(CancellationToken.None);

        Assert.Equal(LoginLaunchOutcome.Failed, result.Outcome);
        Assert.Empty(h.Runner.Calls);
    }

    [Fact]
    public async Task Disable_WhenTaskMissing_SucceedsWithoutElevating()
    {
        var h = new Harness { IsElevated = false };

        var result = await h.Service.DisableAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(h.Elevated.Calls);
    }

    [Fact]
    public async Task Disable_WhenTaskExists_DeletesIt()
    {
        var h = new Harness { IsElevated = true };
        h.Runner.ExistingTaskXml = LoginLaunchTaskXml.Build("u", Exe);

        var result = await h.Service.DisableAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(h.Runner.Calls, c => c.Args.SequenceEqual(["/Delete", "/TN", "Porchlight", "/F"]));
    }

    [Fact]
    public async Task GetState_ParsesTheExistingTask()
    {
        var h = new Harness { IsElevated = true };
        h.Runner.ExistingTaskXml = LoginLaunchTaskXml.Build("u", @"D:\Old\Porchlight.exe");

        var state = await h.Service.GetStateAsync(CancellationToken.None);

        Assert.Equal(new LoginLaunchState(true, @"D:\Old\Porchlight.exe", "--tray"), state);
    }

    [Fact]
    public async Task Refresh_StalePathAndElevated_ReRegisters()
    {
        var h = new Harness { IsElevated = true };
        h.Runner.ExistingTaskXml = LoginLaunchTaskXml.Build("u", @"D:\Old\Porchlight.exe");

        var refreshed = await h.Service.RefreshStaleRegistrationAsync(CancellationToken.None);

        Assert.True(refreshed);
        Assert.Contains(h.Runner.Calls, c => c.Args[0] == "/Create");
        Assert.Contains(Exe, h.Runner.XmlSeenOnCreate, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_StalePathButNotElevated_OnlyLogs()
    {
        var h = new Harness { IsElevated = false };
        h.Runner.ExistingTaskXml = LoginLaunchTaskXml.Build("u", @"D:\Old\Porchlight.exe");

        var refreshed = await h.Service.RefreshStaleRegistrationAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Empty(h.Elevated.Calls);
        Assert.DoesNotContain(h.Runner.Calls, c => c.Args[0] == "/Create");
    }

    [Fact]
    public async Task Refresh_SamePathDifferentCase_DoesNothing()
    {
        var h = new Harness { IsElevated = true };
        h.Runner.ExistingTaskXml = LoginLaunchTaskXml.Build("u", Exe.ToUpperInvariant());

        Assert.False(await h.Service.RefreshStaleRegistrationAsync(CancellationToken.None));
        Assert.DoesNotContain(h.Runner.Calls, c => c.Args[0] == "/Create");
    }

    [Fact]
    public async Task Refresh_NoTask_DoesNothing()
    {
        var h = new Harness { IsElevated = true };

        Assert.False(await h.Service.RefreshStaleRegistrationAsync(CancellationToken.None));
        Assert.DoesNotContain(h.Runner.Calls, c => c.Args[0] == "/Create");
    }

    private sealed class Harness
    {
        private LoginLaunchService? _service;

        public bool IsElevated { get; init; }

        public string? ExePath { get; init; } = Exe;

        public FakeSchtasks Runner { get; } = new();

        public FakeElevatedRunner Elevated { get; } = new();

        public LoginLaunchService Service => _service ??= new LoginLaunchService(
            Runner,
            Elevated,
            new FakeElevation(IsElevated),
            new FakeEnvironment(ExePath),
            NullLogger<LoginLaunchService>.Instance);
    }

    private sealed class FakeElevation(bool isElevated) : IElevationService
    {
        public bool IsElevated => isElevated;

        public bool RestartElevated() => throw new NotSupportedException();
    }

    private sealed class FakeEnvironment(string? exePath) : ILoginLaunchEnvironment
    {
        public string UserId => @"PC\Sam";

        public string? ExePath => exePath;
    }

    private sealed class FakeElevatedRunner : IElevatedCommandRunner
    {
        public List<(string File, string[] Args)> Calls { get; } = [];

        public ElevatedRunResult Next { get; set; } = new(Declined: false, ExitCode: 0);

        public Task<ElevatedRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add((fileName, [.. arguments]));
            return Task.FromResult(Next);
        }
    }

    /// <summary>Stands in for schtasks.exe: /Query answers from <see cref="ExistingTaskXml"/> (exit 1
    /// when null), /Create records the XML file's contents.</summary>
    private sealed class FakeSchtasks : IProcessRunner
    {
        public List<(string File, string[] Args)> Calls { get; } = [];

        public string? ExistingTaskXml { get; set; }

        public int CreateExitCode { get; set; }

        public string XmlSeenOnCreate { get; private set; } = string.Empty;

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IProgress<string>? onLine,
            IProgress<string>? onProgress,
            CancellationToken cancellationToken)
        {
            Calls.Add((fileName, [.. arguments]));
            switch (arguments[0])
            {
                case "/Query":
                    return Task.FromResult(ExistingTaskXml is null
                        ? new ProcessRunResult(1, [], ["ERROR: The system cannot find the file specified."])
                        : new ProcessRunResult(0, ExistingTaskXml.Split(Environment.NewLine), []));
                case "/Create":
                    XmlSeenOnCreate = File.ReadAllText(arguments[4], Encoding.Unicode);
                    return Task.FromResult(new ProcessRunResult(CreateExitCode, [], []));
                default:
                    return Task.FromResult(new ProcessRunResult(0, [], []));
            }
        }

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) => throw new NotSupportedException();
    }
}
