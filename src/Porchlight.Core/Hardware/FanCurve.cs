namespace Porchlight.Core.Hardware;

/// <summary>
/// A validated, sorted set of (temperature, percent) points. <see cref="Evaluate"/> linearly
/// interpolates between points and is flat beyond the first/last point. Pure and stateless -
/// curve hysteresis (only lowering speed once temperature has dropped far enough) is stateful
/// per-fan behaviour and lives in <see cref="FanControlEngine"/> instead.
/// </summary>
public sealed class FanCurve
{
    private readonly IReadOnlyList<FanCurvePoint> _points;

    private FanCurve(IReadOnlyList<FanCurvePoint> points)
    {
        _points = points;
    }

    public IReadOnlyList<FanCurvePoint> Points => _points;

    /// <summary>
    /// Validates and builds a curve. Requires 2-8 points, strictly increasing temperatures, and
    /// non-decreasing percentages that are all within [<paramref name="minPercent"/>, 100].
    /// </summary>
    public static bool TryCreate(
        IReadOnlyList<FanCurvePoint> points,
        double minPercent,
        out FanCurve? curve,
        out string? error)
    {
        curve = null;

        if (points.Count < FanControlOptions.MinCurvePoints || points.Count > FanControlOptions.MaxCurvePoints)
        {
            error = $"A fan curve needs {FanControlOptions.MinCurvePoints}-{FanControlOptions.MaxCurvePoints} points.";
            return false;
        }

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];

            if (!double.IsFinite(point.TemperatureC) || !double.IsFinite(point.Percent))
            {
                error = "Curve points must be finite numbers.";
                return false;
            }

            if (point.Percent < minPercent || point.Percent > 100)
            {
                error = $"Fan percent must be between {minPercent:0} and 100.";
                return false;
            }

            if (i > 0)
            {
                var previous = points[i - 1];
                if (point.TemperatureC <= previous.TemperatureC)
                {
                    error = "Curve temperatures must strictly increase from left to right.";
                    return false;
                }

                if (point.Percent < previous.Percent)
                {
                    error = "Curve percentages cannot decrease from left to right.";
                    return false;
                }
            }
        }

        curve = new FanCurve([.. points]);
        error = null;
        return true;
    }

    /// <summary>Linear interpolation between the two bracketing points; flat at the first point's
    /// percent below its temperature, flat at the last point's percent above its temperature.</summary>
    public double Evaluate(double temperatureC)
    {
        if (temperatureC <= _points[0].TemperatureC)
        {
            return _points[0].Percent;
        }

        var last = _points[^1];
        if (temperatureC >= last.TemperatureC)
        {
            return last.Percent;
        }

        for (var i = 1; i < _points.Count; i++)
        {
            var upper = _points[i];
            if (temperatureC > upper.TemperatureC)
            {
                continue;
            }

            var lower = _points[i - 1];
            var span = upper.TemperatureC - lower.TemperatureC;
            var fraction = span <= 0 ? 0 : (temperatureC - lower.TemperatureC) / span;
            return lower.Percent + (fraction * (upper.Percent - lower.Percent));
        }

        return last.Percent;
    }
}
