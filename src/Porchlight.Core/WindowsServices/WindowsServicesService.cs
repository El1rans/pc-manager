using Porchlight.Core.Components;
using Porchlight.Core.Elevation;
using Porchlight.Core.Startup;

namespace Porchlight.Core.WindowsServices;

/// <inheritdoc cref="IWindowsServicesService"/>
public sealed class WindowsServicesService : IWindowsServicesService
{
    /// <summary>How long Start/Stop wait for the service to reach its new state.</summary>
    public static readonly TimeSpan ChangeTimeout = TimeSpan.FromSeconds(30);

    // AnyDesk's own service name; the Remote support page manages it (the catalog has no name for it).
    private const string AnyDeskServiceName = "AnyDesk";

    private static readonly HashSet<string> ManagedElsewhere = BuildManagedElsewhere();

    private readonly IServiceInfoSource _source;
    private readonly IServiceManager _manager;
    private readonly IFileProductInfoReader _fileInfo;
    private readonly IElevationService _elevation;
    private readonly string _windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    // Names handed out by the last ListAsync; every change refuses anything else.
    private volatile Dictionary<string, WindowsServiceEntry> _known = new(StringComparer.OrdinalIgnoreCase);

    public WindowsServicesService(
        IServiceInfoSource source,
        IServiceManager manager,
        IFileProductInfoReader fileInfo,
        IElevationService elevation)
    {
        _source = source;
        _manager = manager;
        _fileInfo = fileInfo;
        _elevation = elevation;
    }

    public Task<IReadOnlyList<WindowsServiceEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<WindowsServiceEntry>>(() => ListCore(cancellationToken), cancellationToken);

    public Task<ServiceChangeOutcome> StartAsync(string name, CancellationToken cancellationToken) =>
        Task.Run(() => Change(name, () => ServiceChangeOutcome.Of(_manager.Start(name, ChangeTimeout))), cancellationToken);

    public Task<ServiceChangeOutcome> StopAsync(string name, CancellationToken cancellationToken) =>
        Task.Run(() => Change(name, () => StopCore(name)), cancellationToken);

    public Task<ServiceChangeOutcome> RestartAsync(string name, CancellationToken cancellationToken) =>
        Task.Run(() => Change(name, () => RestartCore(name)), cancellationToken);

    public Task<ServiceChangeOutcome> SetStartTypeAsync(string name, ServiceStartType startType, CancellationToken cancellationToken) =>
        Task.Run(() => Change(name, () => ServiceChangeOutcome.Of(_manager.SetStartType(name, startType))), cancellationToken);

    private static HashSet<string> BuildManagedElsewhere()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AnyDeskServiceName };
        foreach (var component in ComponentCatalog.All)
        {
            if (!string.IsNullOrWhiteSpace(component.ServiceName))
            {
                names.Add(component.ServiceName);
            }
        }

        return names;
    }

    private List<WindowsServiceEntry> ListCore(CancellationToken cancellationToken)
    {
        var companies = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<WindowsServiceEntry>();

        foreach (var raw in _source.ReadAll())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ServiceClassifier.IsDriver(raw.ServiceType))
            {
                continue;
            }

            var path = ServiceImagePathParser.ExtractExecutablePath(raw.PathName, _windowsDirectory);
            string? company = null;
            if (path is not null && Path.IsPathRooted(path) && !companies.TryGetValue(path, out company))
            {
                company = _fileInfo.Read(path)?.Company;
                companies[path] = company;
            }

            var isMicrosoft = ServiceClassifier.IsMicrosoft(path, company, _windowsDirectory);
            entries.Add(new WindowsServiceEntry(
                raw.Name,
                raw.DisplayName,
                ServiceClassifier.ShortDescription(raw.Description),
                string.IsNullOrWhiteSpace(company) ? null : company.Trim(),
                ServiceClassifier.ParseStartType(raw.StartMode, raw.DelayedAutoStart),
                ServiceClassifier.ParseState(raw.State),
                isMicrosoft,
                !isMicrosoft && !ManagedElsewhere.Contains(raw.Name)));
        }

        entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        _known = entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        return entries;
    }

    private ServiceChangeOutcome Change(string name, Func<ServiceChangeOutcome> action)
    {
        // Safety rules first, so a Windows service is refused whether or not Porchlight is elevated.
        if (!_known.TryGetValue(name, out var entry) || !entry.IsChangeable)
        {
            return ServiceChangeOutcome.Of(ServiceChangeResult.Refused);
        }

        if (!_elevation.IsElevated)
        {
            return ServiceChangeOutcome.Of(ServiceChangeResult.NeedsAdmin);
        }

        return action();
    }

    private ServiceChangeOutcome StopCore(string name)
    {
        var dependents = _manager.GetRunningDependents(name);
        if (dependents.Count > 0)
        {
            return new ServiceChangeOutcome(ServiceChangeResult.HasDependents, dependents);
        }

        return ServiceChangeOutcome.Of(_manager.Stop(name, ChangeTimeout));
    }

    private ServiceChangeOutcome RestartCore(string name)
    {
        var stopped = StopCore(name);
        if (stopped.Result != ServiceChangeResult.Changed)
        {
            return stopped;
        }

        return ServiceChangeOutcome.Of(_manager.Start(name, ChangeTimeout));
    }
}
