namespace Porchlight.Core.Monitoring;

/// <summary>
/// A fixed-capacity ring buffer of samples used to feed <c>Sparkline</c> charts and to compute
/// min/avg/max over the retained window. Not thread-safe; each sampler/tile owns its own instance
/// and is only ever touched from the thread that advances it.
/// </summary>
public sealed class RollingSeries
{
    private readonly double[] _buffer;
    private int _start;
    private int _count;

    public RollingSeries(int capacity = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _buffer = new double[capacity];
    }

    /// <summary>Maximum number of samples retained; oldest samples are dropped once full.</summary>
    public int Capacity { get; }

    /// <summary>Number of samples currently retained (0 to <see cref="Capacity"/>).</summary>
    public int Count => _count;

    public void Add(double value)
    {
        if (_count < Capacity)
        {
            _buffer[(_start + _count) % Capacity] = value;
            _count++;
        }
        else
        {
            _buffer[_start] = value;
            _start = (_start + 1) % Capacity;
        }
    }

    /// <summary>The retained samples, oldest first. A new array each call, safe to hand to a
    /// data-bound control.</summary>
    public double[] Snapshot()
    {
        var result = new double[_count];
        for (var i = 0; i < _count; i++)
        {
            result[i] = _buffer[(_start + i) % Capacity];
        }

        return result;
    }

    public double Min() => _count == 0 ? 0 : MinCore();

    public double Max() => _count == 0 ? 0 : MaxCore();

    public double Average() => _count == 0 ? 0 : AverageCore();

    private double MinCore()
    {
        var min = double.MaxValue;
        for (var i = 0; i < _count; i++)
        {
            var value = _buffer[(_start + i) % Capacity];
            if (value < min)
            {
                min = value;
            }
        }

        return min;
    }

    private double MaxCore()
    {
        var max = double.MinValue;
        for (var i = 0; i < _count; i++)
        {
            var value = _buffer[(_start + i) % Capacity];
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }

    private double AverageCore()
    {
        var sum = 0.0;
        for (var i = 0; i < _count; i++)
        {
            sum += _buffer[(_start + i) % Capacity];
        }

        return sum / _count;
    }
}
