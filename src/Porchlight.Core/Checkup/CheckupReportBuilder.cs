using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Checkup;

/// <inheritdoc cref="ICheckupReportBuilder"/>
public sealed partial class CheckupReportBuilder : ICheckupReportBuilder
{
    private readonly IReadOnlyList<ICheckupSection> _sections;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CheckupReportBuilder> _logger;

    public CheckupReportBuilder(
        IEnumerable<ICheckupSection> sections, TimeProvider timeProvider, ILogger<CheckupReportBuilder> logger)
    {
        _sections = sections.OrderBy(s => s.Order).ToList();
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CheckupReport> BuildAsync(CancellationToken cancellationToken)
    {
        var results = new List<CheckupSectionResult>();
        foreach (var section in _sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await section.BuildAsync(cancellationToken).ConfigureAwait(false) is { } result)
                {
                    results.Add(result);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One broken section must not stop the whole report; say so instead of hiding it.
                LogSectionFailed(ex, section.Title);
                results.Add(new CheckupSectionResult(
                    section.Title, CheckupSeverity.NeedsAttention, ["Porchlight could not check this."]));
            }
        }

        return new CheckupReport(_timeProvider.GetLocalNow(), Environment.MachineName, results);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Check-up section '{Title}' failed.")]
    private partial void LogSectionFailed(Exception exception, string title);
}
