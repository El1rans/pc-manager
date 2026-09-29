namespace Porchlight.Core.Network;

/// <summary>Turns speed test numbers into a plain-language verdict.</summary>
public static class SpeedVerdictDescriber
{
    private const double FairMinimumMbps = 3;
    private const double GoodMinimumMbps = 10;
    private const double ExcellentMinimumMbps = 50;
    private const double LowUploadMbps = 1;
    private const double HighLatencyMs = 150;

    /// <summary>Describes <paramref name="result"/>, judging mainly by download speed.</summary>
    public static SpeedVerdict Describe(SpeedTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var speed = result.DownloadMbps ?? result.UploadMbps;
        if (speed is null)
        {
            return new SpeedVerdict(SpeedVerdictLevel.Unknown, "Couldn't measure", "The test didn't get an answer. Check your connection and try again.");
        }

        var (level, headline, detail) = speed.Value switch
        {
            < FairMinimumMbps => (SpeedVerdictLevel.Poor, "Very slow",
                "Web pages may take a while and video calls will struggle."),
            < GoodMinimumMbps => (SpeedVerdictLevel.Fair, "Fair",
                "Fine for email, web pages and music. Video calls may be choppy."),
            < ExcellentMinimumMbps => (SpeedVerdictLevel.Good, "Good",
                "Good for video calls, streaming and everyday browsing."),
            _ => (SpeedVerdictLevel.Excellent, "Excellent",
                "Plenty for video calls and streaming, even with several people online."),
        };

        if (result.UploadMbps is < LowUploadMbps)
        {
            detail += " Your upload speed is low, so others may see you in poor quality on video calls.";
        }

        if (result.LatencyMs is > HighLatencyMs)
        {
            detail += " The delay is high, so calls and games may lag.";
        }

        return new SpeedVerdict(level, headline, detail);
    }
}
