namespace Porchlight.Core.Checkup;

/// <summary>Builds a <see cref="CheckupReport"/> from every registered <see cref="ICheckupSection"/>.</summary>
public interface ICheckupReportBuilder
{
    /// <summary>Runs every section. Never throws because of one section; a failing section becomes a
    /// "could not check" entry.</summary>
    Task<CheckupReport> BuildAsync(CancellationToken cancellationToken);
}
