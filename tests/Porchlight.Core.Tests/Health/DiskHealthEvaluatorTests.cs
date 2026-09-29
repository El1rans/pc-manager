using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.Core.Tests.Health;

public class DiskHealthEvaluatorTests
{
    private static DiskHealthInfo Disk(
        int? health = 0, int[]? op = null, int? temp = null, int? wear = null, long? readErrors = null,
        bool? predict = null) =>
        new("Test", 4, 1000, health, op ?? [2], temp, wear, readErrors, predict);

    [Fact]
    public void Healthy_when_windows_says_healthy_and_nothing_else_fires()
    {
        var result = DiskHealthEvaluator.Evaluate(Disk(temp: 40, wear: 5, readErrors: 0));
        Assert.Equal(DiskHealthVerdict.Healthy, result.Verdict);
        Assert.Equal("Healthy", result.VerdictText);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Healthy_with_no_reliability_data_at_all()
    {
        Assert.Equal(DiskHealthVerdict.Healthy, DiskHealthEvaluator.Evaluate(Disk()).Verdict);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(5)]
    public void Unknown_when_health_status_is_missing_or_unknown(int? health)
    {
        var result = DiskHealthEvaluator.Evaluate(Disk(health));
        Assert.Equal(DiskHealthVerdict.Unknown, result.Verdict);
        Assert.Equal("Unknown", result.VerdictText);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Warning_for_warning_or_unhealthy_status(int health)
    {
        var result = DiskHealthEvaluator.Evaluate(Disk(health));
        Assert.Equal(DiskHealthVerdict.Warning, result.Verdict);
        Assert.Equal("Warning - back up your files soon", result.VerdictText);
    }

    [Fact]
    public void Warning_when_failure_is_predicted_even_if_status_is_healthy()
    {
        Assert.Equal(DiskHealthVerdict.Warning, DiskHealthEvaluator.Evaluate(Disk(predict: true)).Verdict);
    }

    [Fact]
    public void Predict_failure_false_is_not_a_warning()
    {
        Assert.Equal(DiskHealthVerdict.Healthy, DiskHealthEvaluator.Evaluate(Disk(predict: false)).Verdict);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(13)]
    public void Warning_for_bad_operational_status(int op)
    {
        Assert.Equal(DiskHealthVerdict.Warning, DiskHealthEvaluator.Evaluate(Disk(op: [2, op])).Verdict);
    }

    [Theory]
    [InlineData(89, DiskHealthVerdict.Healthy)]
    [InlineData(90, DiskHealthVerdict.Warning)]
    public void Wear_threshold(int wear, DiskHealthVerdict expected)
    {
        Assert.Equal(expected, DiskHealthEvaluator.Evaluate(Disk(wear: wear)).Verdict);
    }

    [Theory]
    [InlineData(69, DiskHealthVerdict.Healthy)]
    [InlineData(70, DiskHealthVerdict.Warning)]
    public void Temperature_threshold(int temp, DiskHealthVerdict expected)
    {
        Assert.Equal(expected, DiskHealthEvaluator.Evaluate(Disk(temp: temp)).Verdict);
    }

    [Fact]
    public void Warning_for_uncorrected_read_errors()
    {
        Assert.Equal(DiskHealthVerdict.Warning, DiskHealthEvaluator.Evaluate(Disk(readErrors: 3)).Verdict);
    }

    [Fact]
    public void Multiple_problems_give_multiple_reasons()
    {
        var result = DiskHealthEvaluator.Evaluate(Disk(health: 2, wear: 95, temp: 80));
        Assert.Equal(3, result.Reasons.Count);
    }

    [Theory]
    [InlineData(4, "SSD")]
    [InlineData(3, "Hard disk")]
    [InlineData(0, "Drive")]
    [InlineData(null, "Drive")]
    public void Media_type_text(int? type, string expected)
    {
        Assert.Equal(expected, DiskHealthEvaluator.DescribeMediaType(type));
    }
}
