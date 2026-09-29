namespace Porchlight.Core.Health;

/// <summary>
/// Outcome of one health read (WMI, Event Log, registry): either a value or a short plain-language
/// reason it could not be read. A failed read is shown as "Couldn't check" - it is never thrown at
/// the UI.
/// </summary>
/// <typeparam name="T">The value read.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "Ok/Fail factories read best as HealthReadResult<T>.Ok(...).")]
public sealed record HealthReadResult<T>
{
    private HealthReadResult(bool succeeded, T? value, string? error)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
    }

    /// <summary>True when <see cref="Value"/> holds a reading.</summary>
    public bool Succeeded { get; }

    /// <summary>The reading; only meaningful when <see cref="Succeeded"/>.</summary>
    public T? Value { get; }

    /// <summary>Short plain reason the read failed; null on success.</summary>
    public string? Error { get; }

    public static HealthReadResult<T> Ok(T value) => new(true, value, null);

    public static HealthReadResult<T> Fail(string error) => new(false, default, error);
}
