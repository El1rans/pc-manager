namespace PCManager.App.Features.Hardware;

/// <summary>One entry in the fan curve's source-sensor picker (temperature sensors only).</summary>
public sealed record SensorOption(string Id, string Name);
