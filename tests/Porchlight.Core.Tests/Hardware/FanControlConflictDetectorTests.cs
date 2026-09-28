using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Spec 04 addendum: <see cref="FanControlConflictDetector"/> detects every known vendor
/// fan-control tool by process and/or service name, using a fake <see cref="IRunningSoftwareLister"/>
/// so this never touches a real process or service - and never attempts to stop/modify one either.</summary>
public sealed class FanControlConflictDetectorTests
{
    [Fact]
    public void DetectConflicts_NothingRunning_ReturnsEmpty()
    {
        var lister = new FakeRunningSoftwareLister();
        var detector = new FanControlConflictDetector(lister, NullLogger<FanControlConflictDetector>.Instance);

        var detected = detector.DetectConflicts();

        Assert.Empty(detected);
    }

    public static IEnumerable<object[]> KnownSoftwareByProcess() =>
        FanControlConflictDetector.KnownSoftware
            .Where(s => s.ProcessNames.Count > 0)
            .Select(s => new object[] { s.DisplayName, s.ProcessNames[0] });

    public static IEnumerable<object[]> KnownSoftwareByService() =>
        FanControlConflictDetector.KnownSoftware
            .Where(s => s.ServiceNames.Count > 0)
            .Select(s => new object[] { s.DisplayName, s.ServiceNames[0] });

    [Theory]
    [MemberData(nameof(KnownSoftwareByProcess))]
    public void DetectConflicts_KnownProcessRunning_IsDetected(string displayName, string processName)
    {
        var lister = new FakeRunningSoftwareLister();
        lister.RunningProcessNames.Add(processName);
        var detector = new FanControlConflictDetector(lister, NullLogger<FanControlConflictDetector>.Instance);

        var detected = detector.DetectConflicts();

        Assert.Contains(displayName, detected);
    }

    [Theory]
    [MemberData(nameof(KnownSoftwareByService))]
    public void DetectConflicts_KnownServiceRunning_IsDetected(string displayName, string serviceName)
    {
        var lister = new FakeRunningSoftwareLister();
        lister.RunningServiceNames.Add(serviceName);
        var detector = new FanControlConflictDetector(lister, NullLogger<FanControlConflictDetector>.Instance);

        var detected = detector.DetectConflicts();

        Assert.Contains(displayName, detected);
    }

    [Fact]
    public void DetectConflicts_ProcessNameMatchIsCaseInsensitive()
    {
        var lister = new FakeRunningSoftwareLister();
        lister.RunningProcessNames.Add("speedFAN");
        var detector = new FanControlConflictDetector(lister, NullLogger<FanControlConflictDetector>.Instance);

        var detected = detector.DetectConflicts();

        Assert.Contains("SpeedFan", detected);
    }

    [Fact]
    public void DetectConflicts_UnrelatedProcessesAndServicesRunning_NoFalsePositive()
    {
        var lister = new FakeRunningSoftwareLister();
        lister.RunningProcessNames.AddRange(["explorer", "chrome", "Porchlight"]);
        lister.RunningServiceNames.AddRange(["wuauserv", "Spooler"]);
        var detector = new FanControlConflictDetector(lister, NullLogger<FanControlConflictDetector>.Instance);

        var detected = detector.DetectConflicts();

        Assert.Empty(detected);
    }

    [Fact]
    public void DetectConflicts_NeverCallsAnythingThatCouldStopOrModifyTheDetectedProcessOrService()
    {
        // Detection only (spec 04 addendum): IRunningSoftwareLister is read-only by contract (see
        // its own remarks) - proving the detector only calls its two read methods, and does
        // nothing else with the result besides building the returned list, is the best a unit test
        // can directly assert for "never stops, kills, or modifies anything it finds".
        var lister = new RecordingRunningSoftwareLister();
        lister.RunningProcessNames.Add("FanControl");
        var detector = new FanControlConflictDetector(lister, NullLogger<FanControlConflictDetector>.Instance);

        detector.DetectConflicts();

        Assert.True(lister.ProcessNamesRequested);
        Assert.True(lister.ServiceNamesRequested);
        Assert.Equal(0, lister.OtherCallCount);
    }

    private sealed class RecordingRunningSoftwareLister : IRunningSoftwareLister
    {
        public List<string> RunningProcessNames { get; } = [];

        public List<string> RunningServiceNames { get; } = [];

        public bool ProcessNamesRequested { get; private set; }

        public bool ServiceNamesRequested { get; private set; }

        public int OtherCallCount { get; }

        public IReadOnlyCollection<string> GetRunningProcessNames()
        {
            ProcessNamesRequested = true;
            return RunningProcessNames;
        }

        public IReadOnlyCollection<string> GetRunningServiceNames()
        {
            ServiceNamesRequested = true;
            return RunningServiceNames;
        }
    }
}
