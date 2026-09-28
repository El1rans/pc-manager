namespace Porchlight.Core.Hardware;

/// <inheritdoc cref="IClock"/>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
