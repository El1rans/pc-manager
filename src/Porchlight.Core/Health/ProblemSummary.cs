namespace Porchlight.Core.Health;

/// <summary>One grouped, plain-language row in "Recent problems".</summary>
/// <param name="Category">The kind of problem.</param>
/// <param name="Title">e.g. "Chrome closed unexpectedly 4 times".</param>
/// <param name="Count">How many times it happened.</param>
/// <param name="LastOccurred">When it last happened.</param>
/// <param name="Advice">One line of what to do about it; null when there is nothing useful to add.</param>
public sealed record ProblemSummary(
    ProblemCategory Category, string Title, int Count, DateTimeOffset LastOccurred, string? Advice);
