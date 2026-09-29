namespace Porchlight.Core.Health;

/// <summary>Reads the Event Log records relevant to "Recent problems" (read-only).</summary>
public interface IProblemEventReader
{
    /// <summary>Records from the last <see cref="ProblemSummarizer.WindowDays"/> days.</summary>
    Task<HealthReadResult<IReadOnlyList<HealthEventRecord>>> ReadAsync(CancellationToken cancellationToken);
}
