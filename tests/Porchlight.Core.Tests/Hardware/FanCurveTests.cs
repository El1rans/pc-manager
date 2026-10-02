using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

public sealed class FanCurveTests
{
    private static readonly FanCurvePoint[] ValidPoints =
    [
        new(30, 30),
        new(50, 50),
        new(70, 80),
        new(90, 100),
    ];

    [Fact]
    public void TryCreate_ValidPoints_Succeeds()
    {
        var ok = FanCurve.TryCreate(ValidPoints, minPercent: 30, out var curve, out var error);

        Assert.True(ok);
        Assert.NotNull(curve);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void TryCreate_WrongPointCount_Fails(int count)
    {
        var points = Enumerable.Range(0, count).Select(i => new FanCurvePoint(i * 10, 30 + i)).ToArray();

        var ok = FanCurve.TryCreate(points, minPercent: 20, out var curve, out var error);

        Assert.False(ok);
        Assert.Null(curve);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_TemperaturesNotStrictlyIncreasing_Fails()
    {
        FanCurvePoint[] points = [new(30, 30), new(30, 50), new(70, 80)];

        var ok = FanCurve.TryCreate(points, minPercent: 20, out _, out var error);

        Assert.False(ok);
        Assert.Contains("increase", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreate_TemperaturesDecreasing_Fails()
    {
        FanCurvePoint[] points = [new(50, 30), new(30, 50), new(70, 80)];

        var ok = FanCurve.TryCreate(points, minPercent: 20, out _, out var error);

        Assert.False(ok);
    }

    [Fact]
    public void TryCreate_PercentDecreases_Fails()
    {
        FanCurvePoint[] points = [new(30, 60), new(50, 40), new(70, 80)];

        var ok = FanCurve.TryCreate(points, minPercent: 20, out _, out var error);

        Assert.False(ok);
        Assert.Contains("decrease", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(101)]
    public void TryCreate_PercentOutsideMinAndMax_Fails(double percent)
    {
        FanCurvePoint[] points = [new(30, percent), new(70, 100)];

        var ok = FanCurve.TryCreate(points, minPercent: 20, out _, out var error);

        Assert.False(ok);
    }

    [Theory]
    [InlineData(double.NaN, 30)] // NaN temperature
    [InlineData(30, double.PositiveInfinity)] // infinite percent
    public void TryCreate_NonFiniteValue_Fails(double temperature, double percent)
    {
        FanCurvePoint[] points = [new(temperature, percent), new(70, 100)];

        var ok = FanCurve.TryCreate(points, minPercent: 20, out _, out var error);

        Assert.False(ok);
        Assert.Contains("finite", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_BelowFirstPoint_ReturnsFirstPercent()
    {
        Assert.True(FanCurve.TryCreate(ValidPoints, 30, out var curve, out _));

        Assert.Equal(30, curve!.Evaluate(10));
        Assert.Equal(30, curve.Evaluate(30));
    }

    [Fact]
    public void Evaluate_AboveLastPoint_ReturnsLastPercent()
    {
        Assert.True(FanCurve.TryCreate(ValidPoints, 30, out var curve, out _));

        Assert.Equal(100, curve!.Evaluate(90));
        Assert.Equal(100, curve.Evaluate(150));
    }

    [Fact]
    public void Evaluate_BetweenPoints_LinearlyInterpolates()
    {
        Assert.True(FanCurve.TryCreate(ValidPoints, 30, out var curve, out _));

        // Halfway between (50, 50) and (70, 80) -> 65%.
        Assert.Equal(65, curve!.Evaluate(60), precision: 6);

        // Quarter of the way between (30, 30) and (50, 50) -> 35%.
        Assert.Equal(35, curve.Evaluate(35), precision: 6);
    }

    [Fact]
    public void Evaluate_ExactlyOnAPoint_ReturnsThatPercent()
    {
        Assert.True(FanCurve.TryCreate(ValidPoints, 30, out var curve, out _));

        Assert.Equal(80, curve!.Evaluate(70));
    }
}
