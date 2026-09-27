using Porchlight.Core.Hardware;

namespace Porchlight.Core.Tests.Hardware;

public sealed class FakeFanController : IFanController
{
    public FakeFanController(string id, string name = "Fake fan")
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }

    public double? CurrentPercent { get; set; }

    public bool IsUnderSoftwareControl { get; private set; }

    /// <summary>When false, <see cref="RestoreDefault"/> succeeds (no throw, <see cref="RestoreDefaultCallCount"/>
    /// still increments) but does not clear <see cref="IsUnderSoftwareControl"/> - simulates the known
    /// SuperIO/NVAPI read-back gap where a restore call is accepted without actually leaving software
    /// mode.</summary>
    public bool RestoreDefaultTakesEffect { get; set; } = true;

    public bool CanControl => true;

    public string? RpmSensorId { get; set; }

    public double MinSoftwarePercent { get; set; }

    public double MaxSoftwarePercent { get; set; } = 100;

    public int SetPercentCallCount { get; private set; }

    public int RestoreDefaultCallCount { get; private set; }

    public bool ThrowOnSetPercent { get; set; }

    public bool ThrowOnRestoreDefault { get; set; }

    public void SetPercent(double percent)
    {
        SetPercentCallCount++;
        if (ThrowOnSetPercent)
        {
            throw new InvalidOperationException("Simulated set failure.");
        }

        CurrentPercent = percent;
        IsUnderSoftwareControl = true;
    }

    public void RestoreDefault()
    {
        RestoreDefaultCallCount++;
        if (ThrowOnRestoreDefault)
        {
            throw new InvalidOperationException("Simulated restore failure.");
        }

        CurrentPercent = null;
        if (RestoreDefaultTakesEffect)
        {
            IsUnderSoftwareControl = false;
        }
    }
}
