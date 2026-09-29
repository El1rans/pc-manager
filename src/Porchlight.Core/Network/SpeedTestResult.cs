namespace Porchlight.Core.Network;

/// <summary>Result of a speed test. A null member means that part could not be measured.</summary>
/// <param name="LatencyMs">Typical delay to the test server, in milliseconds.</param>
/// <param name="DownloadMbps">Download speed in megabits per second.</param>
/// <param name="UploadMbps">Upload speed in megabits per second.</param>
/// <param name="MeasuredAt">When the test finished (UTC).</param>
public sealed record SpeedTestResult(double? LatencyMs, double? DownloadMbps, double? UploadMbps, DateTimeOffset MeasuredAt);
