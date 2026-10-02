using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;
using Porchlight.Core.RunningApps;

namespace Porchlight.App.Features.RunningApps;

/// <summary>"Running apps" page: what is running, how much it uses, and a safe way to end it.
/// See docs/specs/28-running-apps.md.</summary>
public sealed partial class RunningAppsViewModel : PageViewModelBase
{
    /// <summary>How often the list refreshes while the page is shown.</summary>
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private const string LoadFailedMessage = "Couldn't read the list of running apps. Porchlight will keep trying.";
    private const string EndTitle = "End task";
    private const string NeedsAdminMessage = "Porchlight needs administrator rights to end this app. Use Restart as administrator in Settings.";
    private const string RefusedMessage = "Windows needs this, so it can't be ended here.";

    private readonly IRunningAppsService _service;
    private readonly IConfirmationDialog _confirmation;
    private readonly IProcessRunner _processRunner;
    private readonly TimeProvider _time;
    private readonly ILogger<RunningAppsViewModel> _logger;
    private readonly Dictionary<string, RunningAppViewModel> _rows = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private RunningAppsSort _selectedSort = RunningAppsSort.Cpu;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasLoaded;

    public RunningAppsViewModel(
        IRunningAppsService service,
        IConfirmationDialog confirmation,
        IProcessRunner processRunner,
        TimeProvider time,
        ILogger<RunningAppsViewModel> logger)
    {
        _service = service;
        _confirmation = confirmation;
        _processRunner = processRunner;
        _time = time;
        _logger = logger;
        Apps = new RunningAppSectionViewModel(RunningAppSection.Apps, "Apps", isExpanded: true);
        Background = new RunningAppSectionViewModel(RunningAppSection.Background, "Background", isExpanded: true);
        Windows = new RunningAppSectionViewModel(RunningAppSection.Windows, "Windows", isExpanded: false);
        Sections = [Apps, Background, Windows];
    }

    public override string Title => "Running apps";

    // Segoe Fluent Icons "TaskView".
    public override string Glyph => "";

    public override int Order => 2;

    public override PageCategory Category => PageCategory.Apps;

    public RunningAppSectionViewModel Apps { get; }

    public RunningAppSectionViewModel Background { get; }

    public RunningAppSectionViewModel Windows { get; }

    public IReadOnlyList<RunningAppSectionViewModel> Sections { get; }

    public IReadOnlyList<RunningAppsSortOption> SortOptions { get; } =
    [
        new(RunningAppsSort.Cpu, "CPU"),
        new(RunningAppsSort.Memory, "Memory"),
        new(RunningAppsSort.Name, "Name"),
    ];

    public bool ShowEmptyState => HasLoaded && Sections.All(s => s.Items.Count == 0);

    /// <summary>Refreshes right away, then every <see cref="RefreshInterval"/> until the token is
    /// cancelled (the shell cancels it when the user navigates elsewhere), so nothing is read while
    /// the page is not shown.</summary>
    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval, _time);
        try
        {
            do
            {
                await RefreshAsync(cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Navigated away or shutting down; polling simply stops.
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyView();

    partial void OnSelectedSortChanged(RunningAppsSort value) => ApplyView();

    [RelayCommand]
    private async Task EndTaskAsync(RunningAppViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        Message = null;
        ErrorMessage = null;
        if (!row.CanEnd)
        {
            ErrorMessage = RefusedMessage;
            return;
        }

        var name = row.Name;
        if (!_confirmation.Confirm(EndTitle, $"End {name}? Unsaved work in it will be lost."))
        {
            return;
        }

        EndTaskResult result;
        try
        {
            result = await _service.EndAsync(row.Key, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            _logger.LogWarning(ex, "Ending {Name} failed unexpectedly.", name);
            result = EndTaskResult.Failed;
        }

        switch (result)
        {
            case EndTaskResult.Ended:
                Message = $"Ended {name}.";
                break;
            case EndTaskResult.NotFound:
                Message = $"{name} is no longer running.";
                break;
            case EndTaskResult.Refused:
                ErrorMessage = RefusedMessage;
                break;
            case EndTaskResult.NeedsAdmin:
                ErrorMessage = NeedsAdminMessage;
                break;
            default:
                ErrorMessage = $"Couldn't end {name}. Try again.";
                break;
        }

        await RefreshAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void OpenFileLocation(RunningAppViewModel? row)
    {
        var path = row?.ExecutablePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            _processRunner.StartDetached("explorer.exe", ["/select,", path]);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            _logger.LogWarning(ex, "Could not open Explorer for {Path}.", path);
            ErrorMessage = "Couldn't open the file location.";
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        RunningAppsSnapshot snapshot;
        try
        {
            snapshot = await _service.SampleAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Reading the running apps failed.");
            ErrorMessage = LoadFailedMessage;
            return;
        }

        Apply(snapshot);
    }

    private void Apply(RunningAppsSnapshot snapshot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in snapshot.Apps)
        {
            seen.Add(app.Key);
            if (_rows.TryGetValue(app.Key, out var row))
            {
                row.Apply(app);
            }
            else
            {
                _rows[app.Key] = new RunningAppViewModel(app);
            }
        }

        foreach (var gone in _rows.Keys.Where(key => !seen.Contains(key)).ToList())
        {
            _rows.Remove(gone);
        }

        Summary = BuildSummary(snapshot);
        if (ErrorMessage == LoadFailedMessage)
        {
            ErrorMessage = null;
        }

        HasLoaded = true;
        ApplyView();
    }

    /// <summary>Makes each section show the matching rows in the chosen order, by moving, inserting
    /// and removing existing row objects rather than rebuilding the lists.</summary>
    private void ApplyView()
    {
        var filter = FilterText.Trim();
        foreach (var section in Sections)
        {
            var desired = Sort(_rows.Values.Where(r => r.Section == section.Section && Matches(r, filter))).ToList();

            for (var i = section.Items.Count - 1; i >= 0; i--)
            {
                if (!desired.Contains(section.Items[i]))
                {
                    section.Items.RemoveAt(i);
                }
            }

            for (var i = 0; i < desired.Count; i++)
            {
                if (i < section.Items.Count && ReferenceEquals(section.Items[i], desired[i]))
                {
                    continue;
                }

                var existing = section.Items.IndexOf(desired[i]);
                if (existing >= 0)
                {
                    section.Items.Move(existing, i);
                }
                else
                {
                    section.Items.Insert(i, desired[i]);
                }
            }

            section.UpdateHeader();
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private static bool Matches(RunningAppViewModel row, string filter) =>
        filter.Length == 0 || row.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    private IEnumerable<RunningAppViewModel> Sort(IEnumerable<RunningAppViewModel> rows) => SelectedSort switch
    {
        RunningAppsSort.Memory => rows.OrderByDescending(r => r.MemoryBytes).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        RunningAppsSort.Name => rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => rows.OrderByDescending(r => r.CpuPercent).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
    };

    private static string BuildSummary(RunningAppsSnapshot snapshot)
    {
        var cpu = string.Create(CultureInfo.InvariantCulture, $"CPU {snapshot.TotalCpuPercent:0}%");
        if (snapshot.Memory is not { } memory || memory.TotalBytes <= 0)
        {
            return cpu;
        }

        return $"{cpu} · Memory {FormatWhole(memory.UsedBytes)} of {FormatWhole(memory.TotalBytes)} in use";
    }

    /// <summary>"16 GB" rather than "16.0 GB" for round numbers.</summary>
    private static string FormatWhole(long bytes) => ByteFormatter.FormatBytes(bytes).Replace(".0 ", " ", StringComparison.Ordinal);
}
