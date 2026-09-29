namespace Porchlight.Core.Alerts;

/// <summary>One alert to show the user, in plain language.</summary>
/// <param name="Kind">What kind of alert this is.</param>
/// <param name="Subject">What it is about (a drive root, "cpu", "gpu", ...); with
/// <paramref name="Kind"/> it is the once-per-24-hours throttle key.</param>
/// <param name="Title">Short balloon title.</param>
/// <param name="Message">Balloon body.</param>
/// <param name="Target">Page opened when the balloon is clicked.</param>
public sealed record Alert(AlertKind Kind, string Subject, string Title, string Message, AlertTarget Target)
{
    /// <summary>The persisted throttle key, <c>"&lt;kind&gt;:&lt;subject&gt;"</c>.</summary>
    public string Key => AlertEvaluator.BuildKey(Kind, Subject);
}
