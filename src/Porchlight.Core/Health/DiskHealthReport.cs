namespace Porchlight.Core.Health;

/// <summary>One physical disk, ready to display.</summary>
/// <param name="Name">Friendly model name.</param>
/// <param name="MediaTypeText">"SSD", "Hard disk", ...</param>
/// <param name="SizeBytes">Size in bytes.</param>
/// <param name="Assessment">Verdict and reasons.</param>
/// <param name="TemperatureCelsius">Temperature when Windows reports one.</param>
/// <param name="WearPercent">Life used, when Windows reports it.</param>
public sealed record DiskHealthReport(
    string Name,
    string MediaTypeText,
    long SizeBytes,
    DiskHealthAssessment Assessment,
    int? TemperatureCelsius,
    int? WearPercent);
